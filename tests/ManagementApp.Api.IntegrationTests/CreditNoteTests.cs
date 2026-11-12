using ManagementApp.Application.Auditing;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Invoices.Queries;
using ManagementApp.Application.Members;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Enums;
using ManagementApp.Domain.Tenancy;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

/// <summary>Credit notes against issued invoices: numbering, effect on the balance, rules, permissions and audit.</summary>
public sealed class CreditNoteTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static IssueCreditNoteCommand Credit(decimal amount, string reason = "Returned goods") =>
        new() { Amount = amount, Date = ApiFactory.Today, Reason = reason };

    [Fact]
    public async Task ACreditNote_IsNumbered_AndReducesTheBalance()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateIssuedInvoiceAsync(TaxedInvoice()); // 100.00 + 20% = 120.00

        var first = await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/credit-notes", Credit(30));
        var second = await (await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/credit-notes", Credit(18, "Late delivery")))
            .ReadAsync<InvoiceDto>();

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var credited = await first.ReadAsync<InvoiceDto>();
        var note = credited.CreditNotes.ShouldHaveSingleItem();
        note.Number.ShouldBe("CN-0001");
        note.Amount.ShouldBe(30m);
        note.TaxAmount.ShouldBe(5m);
        note.NetAmount.ShouldBe(25m);
        credited.CreditedAmount.ShouldBe(30m);
        credited.BalanceDue.ShouldBe(90m);
        credited.Status.ShouldBe(InvoiceStatus.Unpaid);

        second.CreditNotes.Select(c => c.Number).ShouldBe(["CN-0001", "CN-0002"]);
        second.BalanceDue.ShouldBe(72m);
        invoice.InvoiceNumber.ShouldBe("INV-0001"); // separate sequences
    }

    [Fact]
    public async Task CreditingWhatIsLeft_MakesTheInvoiceCredited_AndTotalsNetOfCredits()
    {
        var company = await factory.CreateTenantAsync();
        var credited = await company.Client.CreateIssuedInvoiceAsync(TaxedInvoice());
        var paid = await company.Client.CreatePaidInvoiceAsync(TaxedInvoice(), 100);
        await company.Client.PostJsonAsync($"/api/invoices/{credited.Id}/credit-notes", Credit(120));
        await company.Client.PostJsonAsync($"/api/invoices/{paid.Id}/credit-notes", Credit(20));

        var creditedList = await company.Client.GetInvoicesAsync("/api/invoices?status=Credited");
        var paidList = await company.Client.GetInvoicesAsync("/api/invoices?status=Paid");
        var stats = await company.Client.GetAsync<InvoiceStatsDto>("/api/invoices/stats");

        creditedList.ShouldHaveSingleItem().Id.ShouldBe(credited.Id);
        paidList.ShouldHaveSingleItem().Id.ShouldBe(paid.Id); // part paid, part credited
        stats.OpenCount.ShouldBe(0);
        stats.Currencies.ShouldHaveSingleItem().Billed.ShouldBe(100m); // 240 billed less 140 credited
    }

    [Fact]
    public async Task CreditNotes_CannotExceedTheBalance_AndNeedAReason()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreatePaidInvoiceAsync(TaxedInvoice(), 100);

        var tooMuch = await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/credit-notes", Credit(20.01m));
        var invalid = await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/credit-notes", Credit(0, reason: ""));
        var future = await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/credit-notes",
            Credit(1) with { Date = ApiFactory.Today.AddDays(1) });

        tooMuch.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await tooMuch.ReadAsync<ProblemDetails>()).Detail.ShouldBe("The credit of 20.01 EUR is more than the 20.00 EUR still due.");
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await invalid.ReadAsync<ValidationProblemDetails>()).Errors.Keys.ShouldBe(["amount", "reason"], ignoreOrder: true);
        future.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await company.Client.GetAsync<CurrentTenantDto>("/api/tenant")).Settings.Invoice.CreditNotePrefix.ShouldBe("CN-");
    }

    [Fact]
    public async Task RefusedCreditNotes_LeaveNoGapInTheNumbers()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateIssuedInvoiceAsync(TaxedInvoice());

        await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/credit-notes", Credit(500)); // more than is due
        var accepted = await (await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/credit-notes", Credit(10))).ReadAsync<InvoiceDto>();

        accepted.CreditNotes.ShouldHaveSingleItem().Number.ShouldBe("CN-0001");
    }

    [Fact]
    public async Task DraftsAndVoidInvoices_CannotBeCredited_AndCreditedInvoicesCannotBeVoided()
    {
        var company = await factory.CreateTenantAsync();
        var draft = await company.Client.CreateInvoiceAsync("Draft");
        var toVoid = await company.Client.CreateIssuedInvoiceAsync("Void");
        await company.Client.PostJsonAsync($"/api/invoices/{toVoid.Id}/void", new VoidInvoiceCommand("Duplicate"));
        var credited = await company.Client.CreateIssuedInvoiceAsync("Credited");
        await company.Client.PostJsonAsync($"/api/invoices/{credited.Id}/credit-notes", Credit(10));

        (await company.Client.PostJsonAsync($"/api/invoices/{draft.Id}/credit-notes", Credit(10))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await company.Client.PostJsonAsync($"/api/invoices/{toVoid.Id}/credit-notes", Credit(10))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var voidCredited = await company.Client.PostJsonAsync($"/api/invoices/{credited.Id}/void", new VoidInvoiceCommand("Duplicate"));
        voidCredited.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await voidCredited.ReadAsync<ProblemDetails>()).Detail!.ShouldContain("has credit notes");
    }

    [Fact]
    public async Task TheCreditNotePrefix_IsASetting_AndMustDifferFromTheInvoicePrefix()
    {
        var company = await factory.CreateTenantAsync();
        var settings = (await company.Client.GetAsync<CurrentTenantDto>("/api/tenant")).Settings;

        var clash = await company.Client.PutJsonAsync("/api/tenant/settings",
            settings with { Invoice = settings.Invoice with { CreditNotePrefix = "INV-" } });
        (await company.Client.PutJsonAsync("/api/tenant/settings",
            settings with { Invoice = settings.Invoice with { CreditNotePrefix = "CR/", NumberPadding = 3 } })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var invoice = await company.Client.CreateIssuedInvoiceAsync("Globex");
        var credited = await (await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/credit-notes", Credit(1))).ReadAsync<InvoiceDto>();

        clash.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await clash.ReadAsync<ValidationProblemDetails>()).Errors.ShouldContainKey("invoice.creditNotePrefix");
        credited.CreditNotes.Single().Number.ShouldBe("CR/001");
    }

    [Fact]
    public async Task CreditNotes_NeedEditPermission_AndStayWithinTheCompany()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        var employee = await CreateMemberAsync(mine, TenantRole.Employee);
        var myInvoice = await mine.Client.CreateIssuedInvoiceAsync("Mine");
        var theirInvoice = await theirs.Client.CreateIssuedInvoiceAsync("Theirs");

        (await employee.PostJsonAsync($"/api/invoices/{myInvoice.Id}/credit-notes", Credit(1))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await mine.Client.PostJsonAsync($"/api/invoices/{theirInvoice.Id}/credit-notes", Credit(1))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await theirs.Client.GetAsync<InvoiceDto>($"/api/invoices/{theirInvoice.Id}")).CreditNotes.ShouldBeEmpty();
    }

    [Fact]
    public async Task IssuingACreditNote_IsAudited()
    {
        var company = await factory.CreateTenantAsync();
        var invoice = await company.Client.CreateIssuedInvoiceAsync("Globex");
        await company.Client.PostJsonAsync($"/api/invoices/{invoice.Id}/credit-notes", Credit(12.5m, "Goodwill"));

        var logs = await company.Client.GetAsync<PagedResult<AuditLogDto>>("/api/tenant/audit-logs?action=credit_note.");

        var entry = logs.Items.ShouldHaveSingleItem();
        entry.Action.ShouldBe("credit_note.issued");
        entry.EntityId.ShouldBe(invoice.Id.ToString());
        entry.Details!.ShouldContain("CN-0001");
        entry.Details!.ShouldContain("Goodwill");
    }

    /// <summary>100.00 EUR plus 20% tax: 120.00 EUR.</summary>
    private static SaveInvoiceCommand TaxedInvoice() => NewInvoice("Globex") with
    {
        Items = [new InvoiceItemInput("Consulting", 1, 100m)],
        Tax = 20,
        TaxType = TaxType.Percentage
    };

    private async Task<HttpClient> CreateMemberAsync(TestTenant company, TenantRole role)
    {
        var email = ApiFactory.NewEmail(role.ToString().ToLowerInvariant());
        var created = await company.Client.PostJsonAsync("/api/tenant/users", new CreateMemberCommand(email, null, role, ApiFactory.Password));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await factory.SignInWithBearerAsync(email);
    }
}
