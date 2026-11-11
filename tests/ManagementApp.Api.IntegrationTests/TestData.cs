using ManagementApp.Application.Customers;
using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Application.Products;
using System.Net;
using System.Net.Http.Json;

namespace ManagementApp.Api.IntegrationTests;

internal static class TestData
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static SaveInvoiceCommand NewInvoice(string number) => new()
    {
        InvoiceNumber = number,
        From = "Acme Ltd",
        To = "Globex Corporation",
        IssueDate = new DateOnly(2026, 3, 1),
        DueDate = new DateOnly(2026, 3, 31),
        Currency = "EUR",
        Items = [new InvoiceItemInput("Consulting", 2, 150.50m)]
    };

    public static async Task<InvoiceDto> CreateInvoiceAsync(this HttpClient client, string number) =>
        await client.CreateInvoiceAsync(NewInvoice(number));

    public static async Task<InvoiceDto> CreateInvoiceAsync(this HttpClient client, SaveInvoiceCommand command)
    {
        var response = await client.PostAsJsonAsync("/api/invoices", command, ApiFactory.Json, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsync<InvoiceDto>();
    }

    public static SaveCustomerCommand NewCustomer(string name) => new()
    {
        Name = name,
        Email = "billing@example.com",
        Address = "1 High Street\nSpringfield",
        TaxNumber = "GB999999973",
        Currency = "EUR",
        PaymentTermsDays = 14
    };

    public static async Task<CustomerDto> CreateCustomerAsync(this HttpClient client, string name) =>
        await client.CreateCustomerAsync(NewCustomer(name));

    public static async Task<CustomerDto> CreateCustomerAsync(this HttpClient client, SaveCustomerCommand command)
    {
        var response = await client.PostAsJsonAsync("/api/customers", command, ApiFactory.Json, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsync<CustomerDto>();
    }

    public static SaveProductCommand NewProduct(string name, decimal unitPrice = 95m, string? unit = "hour") =>
        new() { Name = name, UnitPrice = unitPrice, Unit = unit };

    public static async Task<ProductDto> CreateProductAsync(this HttpClient client, string name, decimal unitPrice = 95m)
    {
        var response = await client.PostAsJsonAsync("/api/products", NewProduct(name, unitPrice), ApiFactory.Json, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsync<ProductDto>();
    }

    public static async Task<T> GetAsync<T>(this HttpClient client, string url, Guid? tenantId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (tenantId is { } id)
        {
            request.Headers.Add("X-Tenant-Id", id.ToString());
        }

        var response = await client.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {url}");
        return await response.ReadAsync<T>();
    }

    /// <summary>The items of one page of the invoice list.</summary>
    public static async Task<IReadOnlyList<InvoiceSummaryDto>> GetInvoicesAsync(this HttpClient client, string url, Guid? tenantId = null) =>
        (await client.GetAsync<Application.Tenants.PagedResult<InvoiceSummaryDto>>(url, tenantId)).Items;

    public static async Task<HttpResponseMessage> SendWithTenantAsync(
        this HttpClient client, HttpMethod method, string url, Guid tenantId, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: ApiFactory.Json);
        }

        return await client.SendAsync(request, Ct);
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, ApiFactory.Json, Ct);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, ApiFactory.Json, Ct);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json, Ct))!;
}
