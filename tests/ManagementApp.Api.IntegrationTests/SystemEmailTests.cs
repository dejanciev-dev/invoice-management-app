using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Emails;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Messaging;
using ManagementApp.Infrastructure.Emails;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>Account emails: sent by the platform, from the product's name, and invisible to every company.</summary>
public sealed class SystemEmailTests
{
    [Fact]
    public async Task SystemEmails_AreSentFromTheProduct_WithoutAReplyTo()
    {
        await using var factory = new ApiFactory();
        await QueueAsync(factory, "someone@example.com");

        var sent = await ProcessAsync(factory);

        sent.ShouldBe(1);
        var email = factory.Emails.Sent.ShouldHaveSingleItem();
        email.To.ShouldBe("someone@example.com");
        email.FromName.ShouldBe("Invoice Manager");
        email.ReplyTo.ShouldBeNull();
        (await ProcessAsync(factory)).ShouldBe(0); // sent only once
    }

    [Fact]
    public async Task FailedSystemEmails_AreRetriedLater()
    {
        await using var factory = new ApiFactory();
        var id = await QueueAsync(factory, "someone@example.com");
        factory.Emails.FailWith = "Mail server unreachable";

        var firstAttempt = await ProcessAsync(factory);
        factory.Emails.FailWith = null;
        var tooEarly = await ProcessAsync(factory);
        factory.Clock.Advance(OutboxMessage.RetryDelays[0]);
        var retried = await ProcessAsync(factory);

        firstAttempt.ShouldBe(0);
        tooEarly.ShouldBe(0);
        retried.ShouldBe(1);
        var email = await factory.WithServicesAsync(services =>
            services.GetRequiredService<ApplicationDbContext>().SystemEmailMessages.SingleAsync(e => e.Id == id, Ct));
        email.Status.ShouldBe(EmailStatus.Sent);
        email.Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task SystemEmails_NeverAppearInACompanysEmailLog()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();
        await QueueAsync(factory, company.Email);

        var emails = await company.Client.GetAsync<PagedResult<EmailDto>>("/api/emails");

        emails.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Reset: https://app.example.com/reset-password?email=a%40b.c&code=abc-123_x", "Reset: https://app.example.com/reset-password?email=a%40b.c&code=[redacted]")]
    [InlineData("Join: https://app.example.com/accept-invite?token=Zm9v", "Join: https://app.example.com/accept-invite?token=[redacted]")]
    [InlineData("Nothing secret here.", "Nothing secret here.")]
    public void TheLogSender_RedactsSecretsInLinks(string body, string logged) =>
        LogEmailSender.Redact(body).ShouldBe(logged);

    private static Task<int> ProcessAsync(ApiFactory factory) =>
        factory.Services.GetRequiredService<EmailOutboxProcessor>().ProcessDueAsync(Ct);

    private static Task<int> QueueAsync(ApiFactory factory, string to) =>
        factory.WithServicesAsync(async services =>
        {
            var email = services.GetRequiredService<SystemEmailOutbox>()
                .Queue(userId: null, SystemEmailPurposes.PasswordReset, to, "Reset your password", "Body");
            await services.GetRequiredService<IApplicationDbContext>().SaveChangesAsync(Ct);
            return email.Id;
        });
}
