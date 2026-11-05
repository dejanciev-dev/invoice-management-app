using CsvHelper;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Invoices;
using System.Globalization;

namespace ManagementApp.Infrastructure.Files;

public class CsvFileBuilder : ICsvFileBuilder
{
    private const string IsoDate = "yyyy-MM-dd";

    private static readonly CsvWriterOptions Options = new()
    {
        FormatProvider = CultureInfo.InvariantCulture,
        WriteByteOrderMark = false
    };

    // Columns are written explicitly so the file layout is part of this class, not of the library's record mapping.
    private static readonly string[] Header =
    [
        nameof(InvoiceRecord.InvoiceNumber),
        nameof(InvoiceRecord.To),
        nameof(InvoiceRecord.IssueDate),
        nameof(InvoiceRecord.DueDate),
        nameof(InvoiceRecord.Currency),
        nameof(InvoiceRecord.Subtotal),
        nameof(InvoiceRecord.Discount),
        nameof(InvoiceRecord.Tax),
        nameof(InvoiceRecord.Total),
        nameof(InvoiceRecord.AmountPaid),
        nameof(InvoiceRecord.BalanceDue),
        nameof(InvoiceRecord.Status)
    ];

    public byte[] BuildInvoiceFile(IEnumerable<InvoiceRecord> records)
    {
        using var memoryStream = new MemoryStream();
        using (var csv = CsvWriter.Create(memoryStream, Options, true))
        {
            csv.WriteHeader(Header);

            foreach (var record in records)
            {
                csv.WriteField(record.InvoiceNumber);
                csv.WriteField(record.To);
                csv.WriteField(record.IssueDate, IsoDate);
                csv.WriteField(record.DueDate?.ToString(IsoDate, CultureInfo.InvariantCulture) ?? string.Empty);
                csv.WriteField(record.Currency);
                WriteAmount(csv, record.Subtotal);
                WriteAmount(csv, record.Discount);
                WriteAmount(csv, record.Tax);
                WriteAmount(csv, record.Total);
                WriteAmount(csv, record.AmountPaid);
                WriteAmount(csv, record.BalanceDue);
                csv.WriteField(record.Status.ToString());
                csv.EndRecord();
            }
        }

        return memoryStream.ToArray();
    }

    private static void WriteAmount(CsvWriter csv, decimal amount) =>
        csv.WriteField(amount.ToString(CultureInfo.InvariantCulture));
}
