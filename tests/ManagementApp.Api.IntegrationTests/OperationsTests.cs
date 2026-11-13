using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Emails;
using ManagementApp.Infrastructure.Emails;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using static ManagementApp.Api.IntegrationTests.TestData;

namespace ManagementApp.Api.IntegrationTests;

public sealed class OperationsTests
{
    [Fact]
    public async Task HealthProbes_ReportHealthy()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        foreach (var url in new[] { "/health/live", "/health/ready", "/health" })
        {
            var response = await client.GetAsync(url, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, url);
            (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy", url);
        }
    }

    [Fact]
    public async Task UnwritableFileStorage_MakesTheAppUnready_ButStillLive()
    {
        var notADirectory = Path.GetTempFileName();
        try
        {
            await using var factory = new ApiFactory { Settings = { ["FileStorage:LocalRootPath"] = notADirectory } };
            var client = factory.CreateClient();

            (await client.GetAsync("/health/ready", Ct)).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            (await client.GetAsync("/health/live", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            File.Delete(notADirectory);
        }
    }

    [Fact]
    public async Task EmailsStuckInTheOutbox_ReportDegraded()
    {
        await using var factory = new ApiFactory();
        var company = await factory.CreateTenantAsync();
        await QueueEmailAsync(factory, company.TenantId);

        factory.Clock.Advance(EmailOutboxHealthCheckMaxDelay + TimeSpan.FromMinutes(1));
        var stuck = await factory.CreateClient().GetAsync("/health/ready", Ct);

        stuck.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await stuck.Content.ReadAsStringAsync(Ct)).ShouldBe("Degraded");
    }

    [Fact]
    public async Task BusinessEvents_AreCounted()
    {
        await using var factory = new ApiFactory();
        using var counters = new Counters(factory);

        var company = await factory.CreateTenantAsync();
        await company.Client.CreateIssuedInvoiceAsync("Acme");
        await QueueEmailAsync(factory, company.TenantId);
        await factory.Services.GetRequiredService<EmailOutboxProcessor>().ProcessDueAsync(Ct);

        counters.Total("managementapp.signups").ShouldBe(1);
        counters.Total("managementapp.invoices.issued").ShouldBe(1);
        counters.Total("managementapp.emails.sent").ShouldBeGreaterThanOrEqualTo(1);
    }

    private static readonly TimeSpan EmailOutboxHealthCheckMaxDelay = TimeSpan.FromMinutes(15);

    private static Task<int> QueueEmailAsync(ApiFactory factory, Guid tenantId) =>
        factory.WithServicesAsync(async services =>
        {
            var email = await services.GetRequiredService<EmailOutbox>()
                .QueueAsync(new QueuedEmail(tenantId, "client@example.com", "Hello", "Body"), Ct);
            await services.GetRequiredService<IApplicationDbContext>().SaveChangesAsync(Ct);
            return email.Id;
        });

    /// <summary>Totals of the app's counters, from this factory's meters only (other tests run in parallel).</summary>
    private sealed class Counters : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentDictionary<string, long> _totals = new();

        public Counters(ApiFactory factory)
        {
            var meterFactory = factory.Services.GetRequiredService<IMeterFactory>();
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AppMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, meterFactory))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
                _totals.AddOrUpdate(instrument.Name, value, (_, total) => total + value));
            _listener.Start();
        }

        public long Total(string name) => _totals.GetValueOrDefault(name);

        public void Dispose() => _listener.Dispose();
    }
}
