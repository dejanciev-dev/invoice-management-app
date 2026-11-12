using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Emails;
using ManagementApp.Application.Members;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Messaging;
using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.Emails;
using ManagementApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>The email outbox: queueing, sending with the company's sender details, retries, delivery status.</summary>
/// <remarks>Each test uses its own host, because the email sender and the clock are shared within one.</remarks>
public sealed class EmailOutboxTests
{
    [Fact]
    public async Task QueuedEmails_AreSentWithTheCompanysNameAndReplyTo()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync(companyName: "Northwind Studio");
        await QueueAsync(factory, company.TenantId, "client@example.com", "Hello", "Body text");

        var pending = await company.Client.GetAsync<PagedResult<EmailDto>>("/api/emails");
        var sent = await ProcessAsync(factory);
        var after = await company.Client.GetAsync<PagedResult<EmailDto>>("/api/emails");

        pending.Items.ShouldHaveSingleItem().Status.ShouldBe(EmailStatus.Pending);
        sent.ShouldBe(1);
        var email = factory.Emails.Sent.ShouldHaveSingleItem();
        email.To.ShouldBe("client@example.com");
        email.FromName.ShouldBe("Northwind Studio");
        email.ReplyTo.ShouldBe(company.Email); // the company's email until a reply-to address is set
        email.Subject.ShouldBe("Hello");
        var status = after.Items.ShouldHaveSingleItem();
        status.Status.ShouldBe(EmailStatus.Sent);
        status.SentAt.ShouldBe(factory.Clock.GetUtcNow());
        status.Attempts.ShouldBe(1);
        (await ProcessAsync(factory)).ShouldBe(0); // sent only once
    }

    [Fact]
    public async Task Attachments_AreReadFromTheCompanysStorage()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateIssuedInvoiceAsync("Globex"); // its PDF is stored when issued
        await QueueAsync(factory, company.TenantId, "client@example.com", "Invoice INV-0001", "Attached.",
            ("INV-0001.pdf", "application/pdf", $"invoices/{invoice.Id}/invoice.pdf"));

        await ProcessAsync(factory);

        var attachment = factory.Emails.Sent.ShouldHaveSingleItem().Attachments.ShouldHaveSingleItem();
        attachment.FileName.ShouldBe("INV-0001.pdf");
        attachment.ContentType.ShouldBe("application/pdf");
        System.Text.Encoding.ASCII.GetString(attachment.Content, 0, 5).ShouldBe("%PDF-");
    }

    [Fact]
    public async Task FailedSends_AreRetriedWithGrowingDelays_ThenMarkedFailed_AndCanBeRetried()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();
        await QueueAsync(factory, company.TenantId, "client@example.com", "Hello", "Body");
        factory.Emails.FailWith = "Connection refused";

        await ProcessAsync(factory);
        var afterFirst = (await company.Client.GetAsync<PagedResult<EmailDto>>("/api/emails")).Items.Single();
        (await ProcessAsync(factory)).ShouldBe(0); // not due again yet

        afterFirst.Status.ShouldBe(EmailStatus.Pending);
        afterFirst.Attempts.ShouldBe(1);
        afterFirst.LastError.ShouldBe("Connection refused");
        afterFirst.NextAttemptAt.ShouldBe(factory.Clock.GetUtcNow().AddMinutes(1));

        foreach (var delay in new[] { 1, 5, 15, 60 })
        {
            factory.Clock.Advance(TimeSpan.FromMinutes(delay));
            await ProcessAsync(factory);
        }

        var client = await factory.SignInWithBearerAsync(company.Email); // the first token expired as the clock moved on
        var failed = (await client.GetAsync<PagedResult<EmailDto>>("/api/emails?status=Failed")).Items.ShouldHaveSingleItem();
        failed.Attempts.ShouldBe(EmailMessage.MaxAttempts);

        factory.Emails.FailWith = null;
        (await client.PostAsync($"/api/emails/{failed.Id}/retry", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ProcessAsync(factory)).ShouldBe(1);
        (await client.GetAsync<PagedResult<EmailDto>>("/api/emails")).Items.Single().Status.ShouldBe(EmailStatus.Sent);
    }

    [Fact]
    public async Task AnEmailClaimedByAnotherWorker_IsLeftAlone_UntilItsLeaseRunsOut()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();
        var id = await QueueAsync(factory, company.TenantId, "client@example.com", "Hello", "Body");
        await factory.WithServicesAsync(async services =>
        {
            // Another instance claimed it and then crashed without finishing.
            var context = services.GetRequiredService<ApplicationDbContext>();
            return await context.EmailMessages.IgnoreQueryFilters().Where(e => e.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.LockedUntil, factory.Clock.GetUtcNow() + EmailOutboxProcessor.Lease), Ct);
        });

        (await ProcessAsync(factory)).ShouldBe(0);
        factory.Clock.Advance(EmailOutboxProcessor.Lease + TimeSpan.FromSeconds(1));
        (await ProcessAsync(factory)).ShouldBe(1);
        factory.Emails.Sent.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task TheEmailLog_IsTheCompanysOwn_AndForSettingsManagers()
    {
        await using var factory = new ApiFactory();
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        var theirEmail = await QueueAsync(factory, theirs.TenantId, "client@example.com", "Theirs", "Body");
        var employeeEmail = ApiFactory.NewEmail("employee");
        await mine.Client.PostJsonAsync("/api/tenant/users", new CreateMemberCommand(employeeEmail, null, TenantRole.Employee, ApiFactory.Password));
        var employee = await factory.SignInWithBearerAsync(employeeEmail);

        (await mine.Client.GetAsync<PagedResult<EmailDto>>("/api/emails")).Items.ShouldBeEmpty();
        (await mine.Client.PostAsync($"/api/emails/{theirEmail}/retry", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await employee.GetAsync("/api/emails", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Notifications_GoThroughTheOutbox()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync(companyName: "Contoso");

        await factory.WithServicesAsync(async services =>
        {
            // The real notification sender (the test host captures notifications instead).
            var sender = ActivatorUtilities.CreateInstance<OutboxNotificationSender>(services);
            await sender.SendAsync(new TenantNotification(company.TenantId, "owner@example.com", "3 overdue invoices", "..."), Ct);
            return 0;
        });

        var queued = (await company.Client.GetAsync<PagedResult<EmailDto>>("/api/emails")).Items.ShouldHaveSingleItem();
        queued.Subject.ShouldBe("3 overdue invoices");
        await ProcessAsync(factory);
        factory.Emails.Sent.ShouldHaveSingleItem().FromName.ShouldBe("Contoso");
    }

    private static Task<int> ProcessAsync(ApiFactory factory) =>
        factory.Services.GetRequiredService<EmailOutboxProcessor>().ProcessDueAsync(Ct);

    private static Task<int> QueueAsync(
        ApiFactory factory, Guid tenantId, string to, string subject, string body,
        params (string FileName, string ContentType, string StoragePath)[] attachments) =>
        factory.WithServicesAsync(async services =>
        {
            var outbox = services.GetRequiredService<EmailOutbox>();
            var email = await outbox.QueueAsync(new QueuedEmail(tenantId, to, subject, body) { Attachments = attachments }, Ct);
            await services.GetRequiredService<IApplicationDbContext>().SaveChangesAsync(Ct);
            return email.Id;
        });
}
