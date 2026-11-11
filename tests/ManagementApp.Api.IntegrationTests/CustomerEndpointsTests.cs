using ManagementApp.Application.Auditing;
using ManagementApp.Application.Customers;
using ManagementApp.Application.Members;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class CustomerEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Customers_RequireAuthentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/customers", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCustomer_StoresTrimmedValuesAndBlankOptionalFieldsAsNull()
    {
        var tenant = await factory.CreateTenantAsync();

        var response = await tenant.Client.PostJsonAsync("/api/customers", NewCustomer("  Globex Corporation ") with
        {
            Phone = "   ",
            Notes = " Pays on time "
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.ReadAsync<CustomerDto>();
        response.Headers.Location!.AbsolutePath.ShouldBe($"/api/customers/{created.Id}");

        var customer = await tenant.Client.GetAsync<CustomerDto>($"/api/customers/{created.Id}");
        customer.Name.ShouldBe("Globex Corporation");
        customer.Phone.ShouldBeNull();
        customer.Notes.ShouldBe("Pays on time");
        customer.Address.ShouldBe("1 High Street\nSpringfield");
        customer.Currency.ShouldBe("EUR");
        customer.PaymentTermsDays.ShouldBe(14);
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidData_ReturnsValidationProblem()
    {
        var tenant = await factory.CreateTenantAsync();

        var response = await tenant.Client.PostJsonAsync("/api/customers", NewCustomer(" ") with
        {
            Email = "not-an-email",
            Currency = "euro",
            PaymentTermsDays = -1
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        problem.Errors.Keys.ShouldBe(["name", "email", "currency", "paymentTermsDays"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetCustomers_SortsByNameSearchesAndPages()
    {
        var tenant = await factory.CreateTenantAsync();
        await tenant.Client.CreateCustomerAsync(NewCustomer("Initech") with { Email = "ap@initech.example" });
        await tenant.Client.CreateCustomerAsync(NewCustomer("Acme") with { TaxNumber = "DE123" });
        await tenant.Client.CreateCustomerAsync("Globex");

        var all = await tenant.Client.GetAsync<PagedResult<CustomerDto>>("/api/customers");
        var byEmail = await tenant.Client.GetAsync<PagedResult<CustomerDto>>("/api/customers?search=initech.example");
        var byTaxNumber = await tenant.Client.GetAsync<PagedResult<CustomerDto>>("/api/customers?search=DE123");
        var secondPage = await tenant.Client.GetAsync<PagedResult<CustomerDto>>("/api/customers?page=2&pageSize=2");

        all.Items.Select(c => c.Name).ShouldBe(["Acme", "Globex", "Initech"]);
        all.Total.ShouldBe(3);
        byEmail.Items.ShouldHaveSingleItem().Name.ShouldBe("Initech");
        byTaxNumber.Items.ShouldHaveSingleItem().Name.ShouldBe("Acme");
        secondPage.Items.ShouldHaveSingleItem().Name.ShouldBe("Initech");
        secondPage.Total.ShouldBe(3);
    }

    [Fact]
    public async Task UpdateCustomer_ChangesItAndIsAudited()
    {
        var tenant = await factory.CreateTenantAsync();
        var customer = await tenant.Client.CreateCustomerAsync("Globex");

        var response = await tenant.Client.PutJsonAsync($"/api/customers/{customer.Id}", NewCustomer("Globex Corporation") with
        {
            Currency = null,
            PaymentTermsDays = null
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await tenant.Client.GetAsync<CustomerDto>($"/api/customers/{customer.Id}");
        updated.Name.ShouldBe("Globex Corporation");
        updated.Currency.ShouldBeNull();
        updated.PaymentTermsDays.ShouldBeNull();

        var logs = await tenant.Client.GetAsync<PagedResult<AuditLogDto>>("/api/tenant/audit-logs?action=customer.");
        logs.Items.Select(l => l.Action).ShouldBe(["customer.updated", "customer.created"], ignoreOrder: true);
        logs.Items.ShouldAllBe(l => l.EntityId == customer.Id.ToString());
    }

    [Fact]
    public async Task DeleteCustomer_HidesItEverywhere()
    {
        var tenant = await factory.CreateTenantAsync();
        var customer = await tenant.Client.CreateCustomerAsync("Globex");
        await tenant.Client.CreateCustomerAsync("Initech");

        var deleted = await tenant.Client.DeleteAsync($"/api/customers/{customer.Id}", Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await tenant.Client.GetAsync($"/api/customers/{customer.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await tenant.Client.PutJsonAsync($"/api/customers/{customer.Id}", NewCustomer("Back"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await tenant.Client.DeleteAsync($"/api/customers/{customer.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await tenant.Client.GetAsync<PagedResult<CustomerDto>>("/api/customers")).Items.ShouldHaveSingleItem().Name.ShouldBe("Initech");
    }

    [Fact]
    public async Task AnotherTenantsCustomers_CannotBeSeenOrChanged()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        var secret = await theirs.Client.CreateCustomerAsync("Their Secret Client");
        var url = $"/api/customers/{secret.Id}";

        (await mine.Client.GetAsync<PagedResult<CustomerDto>>("/api/customers")).Items.ShouldBeEmpty();
        (await mine.Client.GetAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.PutJsonAsync(url, NewCustomer("Hijacked"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.DeleteAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.SendWithTenantAsync(HttpMethod.Get, "/api/customers", theirs.TenantId)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await mine.Client.SendWithTenantAsync(HttpMethod.Post, "/api/customers", theirs.TenantId, NewCustomer("Injected")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var untouched = await theirs.Client.GetAsync<PagedResult<CustomerDto>>("/api/customers");
        untouched.Items.ShouldHaveSingleItem().Name.ShouldBe("Their Secret Client");
    }

    [Fact]
    public async Task Customers_UseTheInvoicePermissions()
    {
        var company = await factory.CreateTenantAsync();
        var customer = await company.Client.CreateCustomerAsync("Globex");
        var employee = await CreateMemberAsync(company, TenantRole.Employee); // may view and create invoices only

        (await employee.GetAsync("/api/customers", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await employee.PostJsonAsync("/api/customers", NewCustomer("Initech"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await employee.PutJsonAsync($"/api/customers/{customer.Id}", NewCustomer("Renamed"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await employee.DeleteAsync($"/api/customers/{customer.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> CreateMemberAsync(TestTenant company, TenantRole role)
    {
        var email = ApiFactory.NewEmail(role.ToString().ToLowerInvariant());
        var created = await company.Client.PostJsonAsync("/api/tenant/users", new CreateMemberCommand(email, null, role, ApiFactory.Password));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await factory.SignInWithBearerAsync(email);
    }
}
