using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;

namespace ManagementApp.Domain.UnitTests.Entities;

public class InvoiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 26);

    [Fact]
    public void Subtotal_IsSumOfItemAmounts()
    {
        var invoice = CreateInvoice(Item(2, 150.50m), Item(0.5m, 99.99m));

        invoice.Items[1].Amount.ShouldBe(50.00m); // 49.995 rounds half away from zero
        invoice.Subtotal.ShouldBe(351.00m);
    }

    [Fact]
    public void Total_WithoutDiscountOrTax_EqualsSubtotal()
    {
        var invoice = CreateInvoice(Item(3, 100m));

        invoice.DiscountAmount.ShouldBe(0m);
        invoice.TaxAmount.ShouldBe(0m);
        invoice.Total.ShouldBe(300m);
    }

    [Fact]
    public void PercentageTax_IsAppliedToTheDiscountedSubtotal()
    {
        var invoice = CreateInvoice(Item(2, 150.50m));
        invoice.Discount = 10;
        invoice.DiscountType = DiscountType.Percentage;
        invoice.Tax = 20;
        invoice.TaxType = TaxType.Percentage;

        invoice.Subtotal.ShouldBe(301.00m);
        invoice.DiscountAmount.ShouldBe(30.10m);
        invoice.TaxAmount.ShouldBe(54.18m);
        invoice.Total.ShouldBe(325.08m);
    }

    [Fact]
    public void FlatAmounts_AreUsedAsIs()
    {
        var invoice = CreateInvoice(Item(1, 500m));
        invoice.Discount = 25;
        invoice.DiscountType = DiscountType.Flat;
        invoice.Tax = 12.5m;
        invoice.TaxType = TaxType.Flat;

        invoice.Total.ShouldBe(487.50m);
    }

    [Fact]
    public void FlatDiscount_IsCappedAtTheSubtotal()
    {
        var invoice = CreateInvoice(Item(1, 40m));
        invoice.Discount = 100;
        invoice.DiscountType = DiscountType.Flat;

        invoice.DiscountAmount.ShouldBe(40m);
        invoice.Total.ShouldBe(0m);
    }

    [Fact]
    public void BalanceDue_SubtractsAmountPaid()
    {
        var invoice = CreateInvoice(Item(1, 1000m));
        invoice.AmountPaid = 250m;

        invoice.BalanceDue.ShouldBe(750m);
    }

    [Theory]
    [InlineData(0, null, InvoiceStatus.Unpaid)]
    [InlineData(400, null, InvoiceStatus.PartiallyPaid)]
    [InlineData(1000, null, InvoiceStatus.Paid)]
    [InlineData(1200, -10, InvoiceStatus.Paid)]
    [InlineData(0, -1, InvoiceStatus.Overdue)]
    [InlineData(400, -1, InvoiceStatus.Overdue)]
    [InlineData(0, 0, InvoiceStatus.Unpaid)]
    public void GetStatus_ReflectsPaymentsAndDueDate(decimal amountPaid, int? dueInDays, InvoiceStatus expected)
    {
        var invoice = CreateInvoice(Item(1, 1000m));
        invoice.AmountPaid = amountPaid;
        invoice.DueDate = dueInDays is { } days ? Today.AddDays(days) : null;

        invoice.GetStatus(Today).ShouldBe(expected);
    }

    [Fact]
    public void ReplaceItems_ReplacesExistingItems()
    {
        var invoice = CreateInvoice(Item(1, 10m), Item(1, 20m));

        invoice.ReplaceItems([Item(2, 5m)]);

        invoice.Items.ShouldHaveSingleItem().Amount.ShouldBe(10m);
        invoice.Subtotal.ShouldBe(10m);
    }

    private static Invoice CreateInvoice(params InvoiceItem[] items)
    {
        var invoice = new Invoice
        {
            OwnerId = "owner",
            InvoiceNumber = "INV-001",
            From = "Acme Ltd",
            To = "Globex Corporation",
            IssueDate = Today.AddDays(-14)
        };
        invoice.ReplaceItems(items);
        return invoice;
    }

    private static InvoiceItem Item(decimal quantity, decimal unitPrice) =>
        new() { Description = "Work", Quantity = quantity, UnitPrice = unitPrice };
}
