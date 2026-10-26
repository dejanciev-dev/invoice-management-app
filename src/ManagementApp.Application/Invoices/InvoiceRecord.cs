namespace ManagementApp.Application.Invoices;

public class InvoiceRecord
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; }
    public string Logo { get; set; }
    public string From { get; set; }
    public string To { get; set; }
    public DateTime Date { get; set; }
    public string PaymentTerms { get; set; }
    public DateTime? DueDate { get; set; }
    public double Discount { get; set; }
    public double Tax { get; set; }
    public double AmountPaid { get; set; }
}
