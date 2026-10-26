using CsvHelper.Configuration;
using ManagementApp.Application.Invoices;
using System.Globalization;

namespace ManagementApp.Infrastructure.Files.Maps;

public sealed class InvoiceRecordMap : ClassMap<InvoiceRecord>
{
    public InvoiceRecordMap()
    {
        AutoMap(CultureInfo.InvariantCulture);
    }
}
