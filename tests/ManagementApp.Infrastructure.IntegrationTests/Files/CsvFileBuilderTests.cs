using ManagementApp.Application.Invoices;
using ManagementApp.Domain.Enums;
using ManagementApp.Infrastructure.Files;
using System.Text;

namespace ManagementApp.Infrastructure.IntegrationTests.Files;

public sealed class CsvFileBuilderTests
{
    [Fact]
    public void WritesAHeaderAndOneRowPerInvoice()
    {
        var csv = Build(Record("INV-001", "Globex Corporation", dueDate: new DateOnly(2026, 3, 31)));

        var lines = csv.TrimEnd().Split("\r\n");
        lines.Length.ShouldBe(2);
        lines[0].ShouldBe("InvoiceNumber,To,IssueDate,DueDate,Currency,Subtotal,Discount,Tax,Total,AmountPaid,BalanceDue,Status");
        lines[1].ShouldBe("INV-001,Globex Corporation,2026-03-01,2026-03-31,EUR,301.00,30.10,54.18,325.08,50.00,275.08,PartiallyPaid");
    }

    [Fact]
    public void QuotesFieldsWithLineBreaksCommasAndQuotes()
    {
        var csv = Build(Record("INV-002", "Contoso, Ltd\n1 \"Main\" Street"));

        csv.ShouldContain("INV-002,\"Contoso, Ltd\n1 \"\"Main\"\" Street\",");
    }

    [Fact]
    public void LeavesAMissingDueDateEmpty()
    {
        var csv = Build(Record("INV-003", "Initech", dueDate: null));

        csv.ShouldContain("INV-003,Initech,2026-03-01,,EUR,");
    }

    [Fact]
    public void HasNoByteOrderMark()
    {
        var bytes = new CsvFileBuilder().BuildInvoiceFile([Record("INV-004", "Initech")]);

        bytes[0].ShouldBe((byte)'I');
    }

    private static string Build(params InvoiceRecord[] records) =>
        Encoding.UTF8.GetString(new CsvFileBuilder().BuildInvoiceFile(records));

    private static InvoiceRecord Record(string number, string to, DateOnly? dueDate = null) => new(
        number,
        to,
        new DateOnly(2026, 3, 1),
        dueDate,
        "EUR",
        Subtotal: 301.00m,
        Discount: 30.10m,
        Tax: 54.18m,
        Total: 325.08m,
        AmountPaid: 50.00m,
        BalanceDue: 275.08m,
        InvoiceStatus.PartiallyPaid);
}
