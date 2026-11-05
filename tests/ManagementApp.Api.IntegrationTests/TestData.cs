using ManagementApp.Application.Invoices;
using ManagementApp.Application.Invoices.Commands;
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
