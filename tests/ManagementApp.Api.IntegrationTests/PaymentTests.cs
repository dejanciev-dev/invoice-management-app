using ManagementApp.Application.Auditing;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Members;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Enums;
using ManagementApp.Domain.Tenancy;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>Payments recorded one by one against issued invoices: history, rules, permissions and audit.</summary>
public sealed class PaymentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task PartialPayments_AddUpUntilTheInvoiceIsPaid()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateIssuedInvoiceAsync("Globex"); // 301.00 EUR

        var first = await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/payments",
            NewPayment(100) with { Date = ApiFactory.Today.AddDays(-3), Method = PaymentMethod.Card, Reference = " Visa 4242 ", Notes = "Deposit" });
        var partly = await first.ReadAsync<InvoiceDto>();
        var paid = await company.Client.RecordPaymentAsync(invoice.Id, 201);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        partly.Status.ShouldBe(InvoiceStatus.PartiallyPaid);
        partly.BalanceDue.ShouldBe(201m);
        var deposit = partly.Payments.ShouldHaveSingleItem();
        deposit.ShouldBe(deposit with
        {
            Amount = 100m, Date = ApiFactory.Today.AddDays(-3), Method = PaymentMethod.Card, Reference = "Visa 4242", Notes = "Deposit"
        });
        deposit.RecordedAt.ShouldBe(factory.Clock.GetUtcNow());

        paid.Status.ShouldBe(InvoiceStatus.Paid);
        paid.AmountPaid.ShouldBe(301m);
        paid.BalanceDue.ShouldBe(0m);
        paid.Payments.Select(p => p.Amount).ShouldBe([100m, 201m]); // oldest first
        (await company.Client.GetInvoicesAsync("/api/invoices?status=Paid")).ShouldHaveSingleItem().Id.ShouldBe(invoice.Id);
    }

    [Fact]
    public async Task Payments_CannotExceedTheBalance_OrBeDatedInTheFuture()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateIssuedInvoiceAsync("Globex");
        await company.Client.RecordPaymentAsync(invoice.Id, 300);

        var overpay = await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/payments", NewPayment(1.01m));
        var future = await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/payments",
            NewPayment(1) with { Date = ApiFactory.Today.AddDays(1) });
        var invalid = await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/payments",
            NewPayment(0) with { Reference = new string('x', 101) });

        overpay.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await overpay.ReadAsync<ProblemDetails>()).Detail.ShouldBe("The payment of 1.01 EUR is more than the 1.00 EUR still due.");
        future.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await invalid.ReadAsync<ValidationProblemDetails>()).Errors.Keys.ShouldBe(["amount", "reference"], ignoreOrder: true);
        (await company.Client.GetAsync<InvoiceDto>($"/api/invoices/{invoice.Id}")).Payments.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DraftsAndVoidInvoices_TakeNoPayments()
    {
        var company = await factory.CreateTenantAsync();
        var draft = await company.Client.CreateInvoiceAsync("Draft");
        var issued = await company.Client.CreateIssuedInvoiceAsync("Void");
        var payment = (await company.Client.RecordPaymentAsync(issued.Id, 50)).Payments.Single();
        await company.Client.PostJsonAsync($"/api/invoices/{issued.Id}/void", new VoidInvoiceCommand("Duplicate"));

        var payDraft = await company.Client.PostJsonAsync($"/api/invoices/{draft.Id}/payments", NewPayment(10));
        var payVoid = await company.Client.PostJsonAsync($"/api/invoices/{issued.Id}/payments", NewPayment(10));
        var deleteOnVoid = await company.Client.DeleteAsync($"/api/invoices/{issued.Id}/payments/{payment.Id}", Ct);

        payDraft.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        payVoid.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        deleteOnVoid.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await company.Client.GetAsync<InvoiceDto>($"/api/invoices/{issued.Id}")).Payments.ShouldHaveSingleItem(); // kept on record
    }

    [Fact]
    public async Task DeletingAPayment_PutsTheBalanceBack()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateIssuedInvoiceAsync("Globex");
        await company.Client.RecordPaymentAsync(invoice.Id, 100);
        var mistake = (await company.Client.RecordPaymentAsync(invoice.Id, 201)).Payments.Last();

        var response = await company.Client.DeleteAsync($"/api/invoices/{invoice.Id}/payments/{mistake.Id}", Ct);
        var missing = await company.Client.DeleteAsync($"/api/invoices/{invoice.Id}/payments/{mistake.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.ReadAsync<InvoiceDto>();
        updated.Payments.Select(p => p.Amount).ShouldBe([100m]);
        updated.BalanceDue.ShouldBe(201m);
        updated.Status.ShouldBe(InvoiceStatus.PartiallyPaid);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecordingNeedsEditPermission_AndDeletingNeedsDeletePermission()
    {
        var company = await factory.CreateTenantAsync();
        var employee = await CreateMemberAsync(company, TenantRole.Employee); // view + create
        var accountant = await CreateMemberAsync(company, TenantRole.Accountant); // also edit, but no delete
        var invoice = await company.Client.CreateIssuedInvoiceAsync("Globex");

        (await employee.PostJsonAsync($"/api/invoices/{invoice.Id}/payments", NewPayment(10))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var recorded = await accountant.PostJsonAsync($"/api/invoices/{invoice.Id}/payments", NewPayment(10));
        recorded.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paymentId = (await recorded.ReadAsync<InvoiceDto>()).Payments.Single().Id;

        (await accountant.DeleteAsync($"/api/invoices/{invoice.Id}/payments/{paymentId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await company.Client.DeleteAsync($"/api/invoices/{invoice.Id}/payments/{paymentId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnotherCompanysPayments_CannotBeRecordedOrDeleted()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        var invoice = await theirs.Client.CreatePaidInvoiceAsync(NewInvoice("Theirs"), 100);
        var paymentId = invoice.Payments.Single().Id;

        (await mine.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/payments", NewPayment(10))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.DeleteAsync($"/api/invoices/{invoice.Id}/payments/{paymentId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await theirs.Client.GetAsync<InvoiceDto>($"/api/invoices/{invoice.Id}")).AmountPaid.ShouldBe(100m);
    }

    [Fact]
    public async Task RecordingAndDeletingPayments_IsAudited()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateIssuedInvoiceAsync("Globex");
        var payment = (await company.Client.RecordPaymentAsync(invoice.Id, 120)).Payments.Single();
        await company.Client.DeleteAsync($"/api/invoices/{invoice.Id}/payments/{payment.Id}", Ct);

        var logs = await company.Client.GetAsync<PagedResult<AuditLogDto>>("/api/tenant/audit-logs?action=payment.");

        logs.Items.Select(l => l.Action).ShouldBe(["payment.recorded", "payment.deleted"], ignoreOrder: true);
        logs.Items.ShouldAllBe(l => l.EntityId == invoice.Id.ToString() && l.Details!.Contains("120"));
    }

    private async Task<HttpClient> CreateMemberAsync(TestTenant company, TenantRole role)
    {
        var email = ApiFactory.NewEmail(role.ToString().ToLowerInvariant());
        var created = await company.Client.PostJsonAsync("/api/tenant/users", new CreateMemberCommand(email, null, role, ApiFactory.Password));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await factory.SignInWithBearerAsync(email);
    }
}
