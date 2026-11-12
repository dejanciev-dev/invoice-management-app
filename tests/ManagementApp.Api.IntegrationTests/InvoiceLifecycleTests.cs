using ManagementApp.Application.Auditing;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Invoices.Queries;
using ManagementApp.Application.Members;
using ManagementApp.Application.Reports;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Enums;
using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>Draft → Issued → Void through the API: what each state allows, and that every transition is audited.</summary>
public sealed class InvoiceLifecycleTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task NewInvoices_AreDrafts_ThatCanBeEditedAndDeleted()
    {
        var company = await factory.CreateTenantAsync();
        var draft = await company.Client.CreateInvoiceAsync("INV-001");

        draft.State.ShouldBe(InvoiceState.Draft);
        draft.Status.ShouldBe(InvoiceStatus.Draft);

        (await company.Client.PutJsonAsync($"/api/invoices/{draft.Id}", NewInvoice("INV-001") with { To = "Initech" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await company.Client.DeleteAsync($"/api/invoices/{draft.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task IssuedInvoices_AreFinal()
    {
        var company = await factory.CreateTenantAsync();
        var draft = await company.Client.CreateInvoiceAsync("INV-001");

        var issued = await company.Client.IssueInvoiceAsync(draft.Id);

        issued.State.ShouldBe(InvoiceState.Issued);
        issued.Status.ShouldBe(InvoiceStatus.Unpaid);
        issued.IssuedAt.ShouldBe(factory.Clock.GetUtcNow());

        var edit = await company.Client.PutJsonAsync($"/api/invoices/{draft.Id}", NewInvoice("INV-001") with { To = "Initech" });
        var delete = await company.Client.DeleteAsync($"/api/invoices/{draft.Id}", Ct);
        var issueAgain = await company.Client.PostAsync($"/api/invoices/{draft.Id}/issue", null, Ct);

        edit.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await edit.ReadAsync<ProblemDetails>()).Detail!.ShouldContain("Only drafts can be changed");
        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        issueAgain.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await company.Client.GetAsync<InvoiceDto>($"/api/invoices/{draft.Id}")).To.ShouldBe("INV-001");
    }

    [Fact]
    public async Task Payments_AreRecordedOnIssuedInvoicesOnly()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateInvoiceAsync("INV-001"); // total 301.00

        var onDraft = await company.Client.PutJsonAsync($"/api/invoices/{invoice.Id}/amount-paid", new RecordAmountPaidCommand(100));
        await company.Client.IssueInvoiceAsync(invoice.Id);
        var partly = await company.Client.PutJsonAsync($"/api/invoices/{invoice.Id}/amount-paid", new RecordAmountPaidCommand(100));
        var negative = await company.Client.PutJsonAsync($"/api/invoices/{invoice.Id}/amount-paid", new RecordAmountPaidCommand(-1));

        onDraft.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        partly.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await partly.ReadAsync<InvoiceDto>()).Status.ShouldBe(InvoiceStatus.PartiallyPaid);
        negative.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Voiding_NeedsAReason_AndClosesTheInvoice()
    {
        var company = await factory.CreateTenantAsync();
        var draft = await company.Client.CreateInvoiceAsync("INV-001");

        var voidDraft = await company.Client.PostJsonAsync($"/api/invoices/{draft.Id}/void", new VoidInvoiceCommand("Mistake"));
        await company.Client.IssueInvoiceAsync(draft.Id);
        var noReason = await company.Client.PostJsonAsync($"/api/invoices/{draft.Id}/void", new VoidInvoiceCommand(""));
        var voided = await company.Client.PostJsonAsync($"/api/invoices/{draft.Id}/void", new VoidInvoiceCommand("Billed to the wrong customer"));
        var voidAgain = await company.Client.PostJsonAsync($"/api/invoices/{draft.Id}/void", new VoidInvoiceCommand("Again"));
        var payVoid = await company.Client.PutJsonAsync($"/api/invoices/{draft.Id}/amount-paid", new RecordAmountPaidCommand(10));

        voidDraft.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await voidDraft.ReadAsync<ProblemDetails>()).Detail!.ShouldContain("Delete it instead");
        noReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await noReason.ReadAsync<ValidationProblemDetails>()).Errors.ShouldContainKey("reason");
        voided.StatusCode.ShouldBe(HttpStatusCode.OK);
        var invoice = await voided.ReadAsync<InvoiceDto>();
        invoice.State.ShouldBe(InvoiceState.Void);
        invoice.Status.ShouldBe(InvoiceStatus.Void);
        invoice.VoidedAt.ShouldBe(factory.Clock.GetUtcNow());
        invoice.VoidReason.ShouldBe("Billed to the wrong customer");
        voidAgain.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        payVoid.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await company.Client.DeleteAsync($"/api/invoices/{draft.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DraftsAndVoidInvoices_AreNotOwed()
    {
        var company = await factory.CreateTenantAsync();
        var overdue = NewInvoice("x") with { IssueDate = ApiFactory.Today.AddDays(-40), DueDate = ApiFactory.Today.AddDays(-10) };
        await company.Client.CreateInvoiceAsync(overdue with { To = "DRAFT" });
        await company.Client.CreateIssuedInvoiceAsync(overdue with { To = "ISSUED" });
        var toVoid = await company.Client.CreateIssuedInvoiceAsync(overdue with { To = "VOID" });
        await company.Client.PostJsonAsync($"/api/invoices/{toVoid.Id}/void", new VoidInvoiceCommand("Duplicate"));

        var stats = await company.Client.GetAsync<InvoiceStatsDto>("/api/invoices/stats");
        var summary = await company.Client.GetAsync<FinancialSummaryDto>("/api/reports/summary");
        var overdueList = await company.Client.GetInvoicesAsync("/api/invoices?status=Overdue");
        var voidList = await company.Client.GetInvoicesAsync("/api/invoices?status=Void");

        stats.InvoiceCount.ShouldBe(1);
        stats.OverdueCount.ShouldBe(1);
        stats.DraftCount.ShouldBe(1);
        stats.Currencies.ShouldHaveSingleItem().Billed.ShouldBe(301m);
        summary.Totals.ShouldHaveSingleItem().Overdue.ShouldBe(301m);
        summary.ByStatus.Single(s => s.Status == InvoiceStatus.Draft).Count.ShouldBe(1);
        summary.ByStatus.Single(s => s.Status == InvoiceStatus.Void).Count.ShouldBe(1);
        overdueList.ShouldHaveSingleItem().To.ShouldBe("ISSUED");
        voidList.ShouldHaveSingleItem().To.ShouldBe("VOID");
    }

    [Fact]
    public async Task OverdueReminders_SkipDraftsAndVoidInvoices()
    {
        var company = await factory.CreateTenantAsync();
        var overdue = NewInvoice("x") with { IssueDate = ApiFactory.Today.AddDays(-40), DueDate = ApiFactory.Today.AddDays(-10) };
        await company.Client.CreateInvoiceAsync(overdue with { To = "LATE-DRAFT" });
        var issued = await company.Client.CreateIssuedInvoiceAsync(overdue with { To = "LATE-ISSUED" });
        var toVoid = await company.Client.CreateIssuedInvoiceAsync(overdue with { To = "LATE-VOID" });
        await company.Client.PostJsonAsync($"/api/invoices/{toVoid.Id}/void", new VoidInvoiceCommand("Duplicate"));

        await factory.Services.GetRequiredService<OverdueInvoiceReminderJob>().RunAsync(Ct);

        var reminder = factory.Notifications.Last(n => n.TenantId == company.TenantId);
        reminder.Subject.ShouldBe("1 overdue invoice");
        reminder.Body.ShouldStartWith($"{issued.InvoiceNumber}:");
    }

    [Fact]
    public async Task EveryTransition_IsAudited()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateIssuedInvoiceAsync("INV-001");
        await company.Client.PutJsonAsync($"/api/invoices/{invoice.Id}/amount-paid", new RecordAmountPaidCommand(50));
        await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/void", new VoidInvoiceCommand("Duplicate"));

        var logs = await company.Client.GetAsync<PagedResult<AuditLogDto>>("/api/tenant/audit-logs?action=invoice.");

        logs.Items.Select(l => l.Action).ShouldBe(
            ["invoice.created", "invoice.issued", "invoice.payment_changed", "invoice.voided"], ignoreOrder: true);
        logs.Items.ShouldAllBe(l => l.EntityId == invoice.Id.ToString());
    }

    [Fact]
    public async Task IssuingNeedsEditPermission_AndVoidingNeedsDeletePermission()
    {
        var company = await factory.CreateTenantAsync();
        var employee = await CreateMemberAsync(company, TenantRole.Employee); // view + create
        var accountant = await CreateMemberAsync(company, TenantRole.Accountant); // view + create + edit, no delete
        var draft = await company.Client.CreateInvoiceAsync("INV-001");

        (await employee.PostAsync($"/api/invoices/{draft.Id}/issue", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await accountant.PostAsync($"/api/invoices/{draft.Id}/issue", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await employee.PutJsonAsync($"/api/invoices/{draft.Id}/amount-paid", new RecordAmountPaidCommand(1)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await accountant.PostJsonAsync($"/api/invoices/{draft.Id}/void", new VoidInvoiceCommand("Duplicate")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await company.Client.PostJsonAsync($"/api/invoices/{draft.Id}/void", new VoidInvoiceCommand("Duplicate")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OtherCompaniesInvoices_CannotBeIssuedOrVoided()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        var draft = await theirs.Client.CreateInvoiceAsync("THEIRS-1");
        var issued = await theirs.Client.CreateIssuedInvoiceAsync("THEIRS-2");

        (await mine.Client.PostAsync($"/api/invoices/{draft.Id}/issue", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.PostJsonAsync($"/api/invoices/{issued.Id}/void", new VoidInvoiceCommand("Mine now")))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.PutJsonAsync($"/api/invoices/{issued.Id}/amount-paid", new RecordAmountPaidCommand(301)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await theirs.Client.GetAsync<InvoiceDto>($"/api/invoices/{draft.Id}")).State.ShouldBe(InvoiceState.Draft);
        (await theirs.Client.GetAsync<InvoiceDto>($"/api/invoices/{issued.Id}")).Status.ShouldBe(InvoiceStatus.Unpaid);
    }

    private async Task<HttpClient> CreateMemberAsync(TestTenant company, TenantRole role)
    {
        var email = ApiFactory.NewEmail(role.ToString().ToLowerInvariant());
        var created = await company.Client.PostJsonAsync("/api/tenant/users", new CreateMemberCommand(email, null, role, ApiFactory.Password));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await factory.SignInWithBearerAsync(email);
    }
}
