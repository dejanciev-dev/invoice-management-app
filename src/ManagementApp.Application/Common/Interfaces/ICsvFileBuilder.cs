using ManagementApp.Application.Invoices;

namespace ManagementApp.Application.Common.Interfaces;

public interface ICsvFileBuilder
{
    byte[] BuildInvoiceFile(IEnumerable<InvoiceRecord> records);
}
