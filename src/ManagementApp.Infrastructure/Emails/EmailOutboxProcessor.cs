using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Messaging;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManagementApp.Infrastructure.Emails;

/// <summary>
/// Sends the emails that are due: companies' emails and the platform's account emails. Each email is first claimed with
/// a short lease by a conditional update, so with several API instances running only one of them sends a given email;
/// a crashed sender's lease simply runs out. Delivery is at least once: if the outcome cannot be saved after a
/// successful send, the email may be sent again when the lease expires.
/// </summary>
public sealed class EmailOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<EmailOptions> options,
    AppMetrics metrics,
    ILogger<EmailOutboxProcessor> logger)
{
    public const int BatchSize = 20;
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    /// <summary>Sends what is due and returns how many emails were sent. Account emails go first: someone is waiting.</summary>
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        List<int> dueSystem, due;
        using (var scope = scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = timeProvider.GetUtcNow();
            dueSystem = await DueIds(context.SystemEmailMessages, now, cancellationToken);
            due = await DueIds(context.EmailMessages.IgnoreQueryFilters(), now, cancellationToken);
        }

        var sent = 0;
        foreach (var id in dueSystem)
        {
            sent += await ProcessSystemAsync(id, cancellationToken) ? 1 : 0;
        }

        foreach (var id in due)
        {
            sent += await ProcessAsync(id, cancellationToken) ? 1 : 0;
        }

        return sent;
    }

    /// <summary>Claims and sends one company email; false when another worker had it or the attempt failed.</summary>
    public Task<bool> ProcessAsync(int id, CancellationToken cancellationToken) =>
        ClaimAndSendAsync(
            id,
            // Platform-level work: the email names its tenant, and attachments are read from that tenant's storage only.
            context => context.EmailMessages.IgnoreQueryFilters(),
            async (email, services, ct) =>
            {
                var attachments = new List<OutgoingAttachment>();
                var storage = services.GetRequiredService<IFileStorage>();
                foreach (var attachment in email.Attachments)
                {
                    await using var file = await storage.OpenReadAsync(email.TenantId, attachment.StoragePath, ct)
                        ?? throw new InvalidOperationException($"The attachment {attachment.FileName} is missing.");
                    using var buffer = new MemoryStream();
                    await file.Content.CopyToAsync(buffer, ct);
                    attachments.Add(new OutgoingAttachment(attachment.FileName, attachment.ContentType, buffer.ToArray()));
                }

                return new OutgoingEmail(email.To, email.FromName, email.ReplyTo, email.Subject, email.Body, attachments);
            },
            cancellationToken);

    /// <summary>Claims and sends one account email, from the product's own name.</summary>
    public Task<bool> ProcessSystemAsync(int id, CancellationToken cancellationToken) =>
        ClaimAndSendAsync(
            id,
            context => context.SystemEmailMessages,
            (email, _, _) => Task.FromResult(new OutgoingEmail(
                email.To, options.Value.SystemFromName, ReplyTo: null, email.Subject, email.Body, [])),
            cancellationToken);

    private async Task<bool> ClaimAndSendAsync<TMessage>(
        int id,
        Func<ApplicationDbContext, IQueryable<TMessage>> messages,
        Func<TMessage, IServiceProvider, CancellationToken, Task<OutgoingEmail>> build,
        CancellationToken cancellationToken) where TMessage : OutboxMessage
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = timeProvider.GetUtcNow();

        var claimed = await Due(messages(context), now)
            .Where(e => e.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.LockedUntil, now + Lease), cancellationToken);
        if (claimed == 0)
        {
            return false;
        }

        var email = await messages(context).SingleAsync(e => e.Id == id, cancellationToken);
        try
        {
            var outgoing = await build(email, scope.ServiceProvider, cancellationToken);
            var providerMessageId = await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(outgoing, cancellationToken);
            email.MarkSent(timeProvider.GetUtcNow(), providerMessageId);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            email.MarkAttemptFailed(timeProvider.GetUtcNow(), e.Message);
            logger.LogWarning(e, "Sending {MessageType} {EmailId} failed (attempt {Attempt})", typeof(TMessage).Name, email.Id, email.Attempts);
        }

        // Saved even if the request is being cancelled, so the outcome of a send that happened is not lost.
        await context.SaveChangesAsync(CancellationToken.None);
        metrics.EmailAttempted(email.Status == EmailStatus.Sent);
        return email.Status == EmailStatus.Sent;
    }

    private static Task<List<int>> DueIds<TMessage>(IQueryable<TMessage> messages, DateTimeOffset now, CancellationToken cancellationToken)
        where TMessage : OutboxMessage =>
        Due(messages, now).OrderBy(e => e.NextAttemptAt).Select(e => e.Id).Take(BatchSize).ToListAsync(cancellationToken);

    private static IQueryable<TMessage> Due<TMessage>(IQueryable<TMessage> messages, DateTimeOffset now) where TMessage : OutboxMessage =>
        messages.Where(e => e.Status == EmailStatus.Pending && e.NextAttemptAt <= now && (e.LockedUntil == null || e.LockedUntil < now));
}

/// <summary>Runs the outbox processor every few seconds. Off when <c>Email:WorkerEnabled</c> or background jobs are off.</summary>
public sealed class EmailOutboxWorker(
    EmailOutboxProcessor processor,
    IOptions<EmailOptions> options,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled || !configuration.GetValue("BackgroundJobs:Enabled", true))
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollSeconds)), timeProvider);
        do
        {
            try
            {
                var sent = await processor.ProcessDueAsync(stoppingToken);
                if (sent > 0)
                {
                    logger.LogInformation("Sent {Count} emails", sent);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "The email outbox could not be processed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public static class EmailRegistration
{
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(o => o.Provider is "Log" or "Smtp", "Email:Provider must be Log or Smtp.")
            .Validate(o => MimeKit.MailboxAddress.TryParse(o.FromAddress, out _), "Email:FromAddress must be an email address.")
            .Validate(o => o.Provider != "Smtp" || !string.IsNullOrWhiteSpace(o.Smtp.Host), "Email:Smtp:Host is required for the Smtp provider.")
            .ValidateOnStart();

        services.AddScoped<IEmailSender>(provider =>
            provider.GetRequiredService<IOptions<EmailOptions>>().Value.Provider == "Smtp"
                ? ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider)
                : ActivatorUtilities.CreateInstance<LogEmailSender>(provider));

        services.AddSingleton<EmailOutboxProcessor>();
        services.AddHostedService<EmailOutboxWorker>();
        return services;
    }
}
