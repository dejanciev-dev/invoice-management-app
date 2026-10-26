using CsvHelper;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Invoices;
using System.Globalization;

namespace ManagementApp.Infrastructure.Files;

public class CsvFileBuilder : ICsvFileBuilder
{
    public byte[] BuildInvoiceFile(IEnumerable<InvoiceRecord> records)
    {
        using var memoryStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(memoryStream))
        using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
        {
            string[] isoDate = ["yyyy-MM-dd"];
            csvWriter.Context.TypeConverterOptionsCache.GetOptions<DateOnly>().Formats = isoDate;
            csvWriter.Context.TypeConverterOptionsCache.GetOptions<DateOnly?>().Formats = isoDate;

            csvWriter.WriteRecords(records);
        }

        return memoryStream.ToArray();
    }
}
