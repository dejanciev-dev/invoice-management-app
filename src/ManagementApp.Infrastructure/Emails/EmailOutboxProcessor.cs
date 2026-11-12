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
/// Sends the emails that are due, across all companies. Each email is first claimed with a short lease by a
/// conditional update, so with several API instances running only one of them sends a given email; a crashed sender's
/// lease simply runs out. Delivery is at least once: if the outcome cannot be saved after a successful send, the email
/// may be sent again when the lease expires.
/// </summary>
public sealed class EmailOutboxProcessor(
    IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<EmailOutboxProcessor> logger)
{
    public const int BatchSize = 20;
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    /// <summary>Sends what is due and returns how many emails were sent.</summary>
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        List<int> due;
        using (var scope = scopeFactory.CreateScope())
        {
            var now = timeProvider.GetUtcNow();
            due = await Due(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), now)
                .OrderBy(e => e.NextAttemptAt)
                .Select(e => e.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
        }

        var sent = 0;
        foreach (var id in due)
        {
            if (await ProcessAsync(id, cancellationToken))
            {
                sent++;
            }
        }

        return sent;
    }

    /// <summary>Claims and sends one email; false when another worker had it or the attempt failed.</summary>
    public async Task<bool> ProcessAsync(int id, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = timeProvider.GetUtcNow();

        var claimed = await Due(context, now)
            .Where(e => e.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.LockedUntil, now + Lease), cancellationToken);
        if (claimed == 0)
        {
            return false;
        }

        // Platform-level work: the email names its tenant, and attachments are read from that tenant's storage only.
        var email = await context.EmailMessages.IgnoreQueryFilters().SingleAsync(e => e.Id == id, cancellationToken);
        try
        {
            var attachments = new List<OutgoingAttachment>();
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
            foreach (var attachment in email.Attachments)
            {
                await using var file = await storage.OpenReadAsync(email.TenantId, attachment.StoragePath, cancellationToken)
                    ?? throw new InvalidOperationException($"The attachment {attachment.FileName} is missing.");
                using var buffer = new MemoryStream();
                await file.Content.CopyToAsync(buffer, cancellationToken);
                attachments.Add(new OutgoingAttachment(attachment.FileName, attachment.ContentType, buffer.ToArray()));
            }

            var providerMessageId = await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(
                new OutgoingEmail(email.To, email.FromName, email.ReplyTo, email.Subject, email.Body, attachments),
                cancellationToken);
            email.MarkSent(timeProvider.GetUtcNow(), providerMessageId);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            email.MarkAttemptFailed(timeProvider.GetUtcNow(), e.Message);
            logger.LogWarning(e, "Sending email {EmailId} for tenant {TenantId} failed (attempt {Attempt})",
                email.Id, email.TenantId, email.Attempts);
        }

        // Saved even if the request is being cancelled, so the outcome of a send that happened is not lost.
        await context.SaveChangesAsync(CancellationToken.None);
        return email.Status == EmailStatus.Sent;
    }

    private static IQueryable<EmailMessage> Due(ApplicationDbContext context, DateTimeOffset now) =>
        context.EmailMessages.IgnoreQueryFilters().Where(e =>
            e.Status == EmailStatus.Pending && e.NextAttemptAt <= now && (e.LockedUntil == null || e.LockedUntil < now));
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
