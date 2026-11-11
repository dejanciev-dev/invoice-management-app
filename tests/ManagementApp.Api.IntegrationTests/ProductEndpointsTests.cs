using ManagementApp.Application.Auditing;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Members;
using ManagementApp.Application.Products;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class ProductEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Products_RequireAuthentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/products", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateProduct_StoresTrimmedValuesAndABlankUnitAsNull()
    {
        var tenant = await factory.CreateTenantAsync();

        var response = await tenant.Client.PostJsonAsync("/api/products", NewProduct("  Logo design ", 450m, unit: "  "));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.ReadAsync<ProductDto>();
        response.Headers.Location!.AbsolutePath.ShouldBe($"/api/products/{created.Id}");
        var product = await tenant.Client.GetAsync<ProductDto>($"/api/products/{created.Id}");
        product.Name.ShouldBe("Logo design");
        product.Unit.ShouldBeNull();
        product.UnitPrice.ShouldBe(450m);
    }

    [Fact]
    public async Task CreateProduct_WithInvalidData_ReturnsValidationProblem()
    {
        var tenant = await factory.CreateTenantAsync();

        var response = await tenant.Client.PostJsonAsync("/api/products", NewProduct(" ", -1m, unit: new string('x', 21)));
        var tooPrecise = await tenant.Client.PostJsonAsync("/api/products", NewProduct("Hosting", 9.999m));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ValidationProblemDetails>()).Errors.Keys.ShouldBe(["name", "unitPrice", "unit"], ignoreOrder: true);
        tooPrecise.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProducts_SortsByNameSearchesAndPages()
    {
        var tenant = await factory.CreateTenantAsync();
        await tenant.Client.CreateProductAsync("Web hosting");
        await tenant.Client.CreateProductAsync("Consulting");
        await tenant.Client.CreateProductAsync("Logo design");

        var all = await tenant.Client.GetAsync<PagedResult<ProductDto>>("/api/products");
        var search = await tenant.Client.GetAsync<PagedResult<ProductDto>>("/api/products?search=host");
        var secondPage = await tenant.Client.GetAsync<PagedResult<ProductDto>>("/api/products?page=2&pageSize=2");

        all.Items.Select(p => p.Name).ShouldBe(["Consulting", "Logo design", "Web hosting"]);
        search.Items.ShouldHaveSingleItem().Name.ShouldBe("Web hosting");
        secondPage.Items.ShouldHaveSingleItem().Name.ShouldBe("Web hosting");
        secondPage.Total.ShouldBe(3);
    }

    [Fact]
    public async Task UpdatingAProduct_IsAuditedAndLeavesInvoicesAlone()
    {
        var tenant = await factory.CreateTenantAsync();
        var product = await tenant.Client.CreateProductAsync("Consulting", 95m);
        var invoice = await tenant.Client.CreateInvoiceAsync(NewInvoice("INV-001") with
        {
            Items = [new InvoiceItemInput(product.Name, 2, product.UnitPrice)]
        });

        var response = await tenant.Client.PutJsonAsync($"/api/products/{product.Id}", NewProduct("Senior consulting", 120m));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<ProductDto>()).UnitPrice.ShouldBe(120m);
        var unchanged = await tenant.Client.GetAsync<InvoiceDto>($"/api/invoices/{invoice.Id}");
        unchanged.Items.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            item => item.Description.ShouldBe("Consulting"),
            item => item.UnitPrice.ShouldBe(95m));
        var logs = await tenant.Client.GetAsync<PagedResult<AuditLogDto>>("/api/tenant/audit-logs?action=product.");
        logs.Items.Select(l => l.Action).ShouldBe(["product.updated", "product.created"], ignoreOrder: true);
    }

    [Fact]
    public async Task DeleteProduct_RemovesItFromTheCatalogue()
    {
        var tenant = await factory.CreateTenantAsync();
        var product = await tenant.Client.CreateProductAsync("Consulting");
        await tenant.Client.CreateProductAsync("Hosting");

        var deleted = await tenant.Client.DeleteAsync($"/api/products/{product.Id}", Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await tenant.Client.GetAsync($"/api/products/{product.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await tenant.Client.PutJsonAsync($"/api/products/{product.Id}", NewProduct("Back"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await tenant.Client.GetAsync<PagedResult<ProductDto>>("/api/products")).Items.ShouldHaveSingleItem().Name.ShouldBe("Hosting");
    }

    [Fact]
    public async Task AnotherTenantsProducts_CannotBeSeenOrChanged()
    {
        var mine = await factory.CreateTenantAsync();
        var theirs = await factory.CreateTenantAsync();
        var secret = await theirs.Client.CreateProductAsync("Their Secret Service");
        var url = $"/api/products/{secret.Id}";

        (await mine.Client.GetAsync<PagedResult<ProductDto>>("/api/products")).Items.ShouldBeEmpty();
        (await mine.Client.GetAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.PutJsonAsync(url, NewProduct("Hijacked"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.DeleteAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await mine.Client.SendWithTenantAsync(HttpMethod.Get, "/api/products", theirs.TenantId)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await theirs.Client.GetAsync<ProductDto>(url)).Name.ShouldBe("Their Secret Service");
    }

    [Fact]
    public async Task Products_UseTheInvoicePermissions()
    {
        var company = await factory.CreateTenantAsync();
        var product = await company.Client.CreateProductAsync("Consulting");
        var employee = await CreateMemberAsync(company, TenantRole.Employee); // may view and create invoices only

        (await employee.GetAsync("/api/products", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await employee.PostJsonAsync("/api/products", NewProduct("Hosting"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await employee.PutJsonAsync($"/api/products/{product.Id}", NewProduct("Renamed"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await employee.DeleteAsync($"/api/products/{product.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> CreateMemberAsync(TestTenant company, TenantRole role)
    {
        var email = ApiFactory.NewEmail(role.ToString().ToLowerInvariant());
        var created = await company.Client.PostJsonAsync("/api/tenant/users", new CreateMemberCommand(email, null, role, ApiFactory.Password));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await factory.SignInWithBearerAsync(email);
    }
}
