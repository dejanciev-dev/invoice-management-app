using System.Diagnostics.Metrics;

namespace ManagementApp.Application.Common;

/// <summary>
/// Business and operational counters, published on the <c>ManagementApp</c> meter. Read them with
/// <c>dotnet-counters monitor -n ManagementApp.Api --counters ManagementApp</c> or any OpenTelemetry exporter.
/// Tags never carry tenant or personal data.
/// </summary>
public sealed class AppMetrics
{
    public const string MeterName = "ManagementApp";

    private readonly Counter<long> _signups;
    private readonly Counter<long> _invoicesIssued;
    private readonly Counter<long> _emails;
    private readonly Counter<long> _jobRuns;

    public AppMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _signups = meter.CreateCounter<long>("managementapp.signups", description: "New accounts with their own company");
        _invoicesIssued = meter.CreateCounter<long>("managementapp.invoices.issued", description: "Invoices issued");
        _emails = meter.CreateCounter<long>("managementapp.emails.sent", description: "Send attempts from the email outbox, by outcome");
        _jobRuns = meter.CreateCounter<long>("managementapp.jobs.runs", description: "Scheduled job runs, by job and outcome");
    }

    public void SignedUp() => _signups.Add(1);

    public void InvoiceIssued() => _invoicesIssued.Add(1);

    public void EmailAttempted(bool succeeded) => _emails.Add(1, Outcome(succeeded));

    public void JobRan(string job, bool succeeded) => _jobRuns.Add(1, new KeyValuePair<string, object?>("job", job), Outcome(succeeded));

    private static KeyValuePair<string, object?> Outcome(bool succeeded) => new("outcome", succeeded ? "success" : "failure");
}
