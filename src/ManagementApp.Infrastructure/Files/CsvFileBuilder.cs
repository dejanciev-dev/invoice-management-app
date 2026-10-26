using CsvHelper;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Entities;
using ManagementApp.Infrastructure.Files.Maps;
using System.Globalization;

namespace ManagementApp.Infrastructure.Files
{
    public class CsvFileBuilder : ICsvFileBuilder
    {
        public byte[] BuildInvoiceFile(IEnumerable<Invoice> records)
        {
            using var memoryStream = new MemoryStream();
            using (var streamWriter = new StreamWriter(memoryStream))
            {
                using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

                csvWriter.Context.RegisterClassMap<InvoiceRecordMap>();
                csvWriter.WriteRecords(records);
            }

            return memoryStream.ToArray();
        }
    }
}
