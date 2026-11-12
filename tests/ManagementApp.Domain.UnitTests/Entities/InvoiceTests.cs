using ManagementApp.Domain.Common;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Enums;

namespace ManagementApp.Domain.UnitTests.Entities;

public class InvoiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 26);
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 30, 0, TimeSpan.Zero);

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
    public void BalanceDue_SubtractsThePayments()
    {
        var invoice = IssuedInvoice(Item(1, 1000m));
        Pay(invoice, 250m);
        Pay(invoice, 100m);

        invoice.AmountPaid.ShouldBe(350m);
        invoice.BalanceDue.ShouldBe(650m);
        invoice.Payments.Select(p => p.Amount).ShouldBe([250m, 100m]);
    }

    [Theory]
    [InlineData(0, null, InvoiceStatus.Unpaid)]
    [InlineData(400, null, InvoiceStatus.PartiallyPaid)]
    [InlineData(1000, null, InvoiceStatus.Paid)]
    [InlineData(1000, -10, InvoiceStatus.Paid)]
    [InlineData(0, -1, InvoiceStatus.Overdue)]
    [InlineData(400, -1, InvoiceStatus.Overdue)]
    [InlineData(0, 0, InvoiceStatus.Unpaid)]
    public void GetStatus_OfAnIssuedInvoice_ReflectsPaymentsAndDueDate(decimal amountPaid, int? dueInDays, InvoiceStatus expected)
    {
        var invoice = CreateInvoice(Item(1, 1000m));
        invoice.DueDate = dueInDays is { } days ? Today.AddDays(days) : null;
        invoice.Issue(Now, "INV-001");
        if (amountPaid > 0)
        {
            Pay(invoice, amountPaid);
        }

        invoice.GetStatus(Today).ShouldBe(expected);
    }

    [Fact]
    public void ANewInvoice_IsAnEditableDraft_WhateverItsDueDate()
    {
        var invoice = CreateInvoice(Item(1, 1000m));
        invoice.DueDate = Today.AddDays(-30);

        invoice.State.ShouldBe(InvoiceState.Draft);
        invoice.InvoiceNumber.ShouldBeNull();
        invoice.GetStatus(Today).ShouldBe(InvoiceStatus.Draft);
        Should.NotThrow(invoice.EnsureEditable);
        Should.NotThrow(invoice.EnsureDeletable);
    }

    [Fact]
    public void Issue_MakesTheInvoiceFinal()
    {
        var invoice = CreateInvoice(Item(1, 1000m));

        invoice.Issue(Now, "INV-001");

        invoice.State.ShouldBe(InvoiceState.Issued);
        invoice.IssuedAt.ShouldBe(Now);
        invoice.InvoiceNumber.ShouldBe("INV-001");
        Should.Throw<DomainRuleException>(invoice.EnsureEditable).Message.ShouldContain("Only drafts can be changed");
        Should.Throw<DomainRuleException>(invoice.EnsureDeletable).Message.ShouldContain("Void it instead");
        Should.Throw<DomainRuleException>(() => invoice.Issue(Now, "INV-002")).Message.ShouldContain("already issued");
        invoice.InvoiceNumber.ShouldBe("INV-001");
    }

    [Fact]
    public void Issue_RequiresAtLeastOneItem()
    {
        var invoice = CreateInvoice();

        Should.Throw<DomainRuleException>(() => invoice.Issue(Now, "INV-001"));
        invoice.State.ShouldBe(InvoiceState.Draft);
    }

    [Fact]
    public void Void_ClosesAnIssuedInvoiceForGood()
    {
        var invoice = CreateInvoice(Item(1, 1000m));
        invoice.Issue(Now, "INV-001");

        invoice.Void(Now.AddDays(1), "  Billed to the wrong customer ");

        invoice.State.ShouldBe(InvoiceState.Void);
        invoice.VoidedAt.ShouldBe(Now.AddDays(1));
        invoice.VoidReason.ShouldBe("Billed to the wrong customer");
        invoice.GetStatus(Today).ShouldBe(InvoiceStatus.Void);
        Should.Throw<DomainRuleException>(() => invoice.Void(Now, "again")).Message.ShouldContain("already void");
        Should.Throw<DomainRuleException>(() => Pay(invoice, 10));
        Should.Throw<DomainRuleException>(invoice.EnsureEditable);
        Should.Throw<DomainRuleException>(invoice.EnsureDeletable);
    }

    [Fact]
    public void Void_IsNotForDrafts_AndNeedsAReason()
    {
        var draft = CreateInvoice(Item(1, 1000m));
        Should.Throw<DomainRuleException>(() => draft.Void(Now, "Mistake")).Message.ShouldContain("Delete it instead");

        draft.Issue(Now, "INV-001");
        Should.Throw<DomainRuleException>(() => draft.Void(Now, " "));
        draft.State.ShouldBe(InvoiceState.Issued);
    }

    [Fact]
    public void Payments_AreOnlyForIssuedInvoices()
    {
        var invoice = CreateInvoice(Item(1, 1000m));
        Should.Throw<DomainRuleException>(() => Pay(invoice, 100)).Message.ShouldContain("a draft");

        invoice.Issue(Now, "INV-001");
        var payment = invoice.RecordPayment(1000, Today, PaymentMethod.Card, "  **** 4242 ", " ", Today);

        invoice.GetStatus(Today).ShouldBe(InvoiceStatus.Paid);
        payment.Date.ShouldBe(Today);
        payment.Method.ShouldBe(PaymentMethod.Card);
        payment.Reference.ShouldBe("**** 4242");
        payment.Notes.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 0, "more than zero")]
    [InlineData(-5, 0, "more than zero")]
    [InlineData(1000.01, 0, "more than the 1000.00 EUR still due")]
    [InlineData(100, 1, "future")]
    public void Payments_MustBePositive_NotMoreThanIsDue_AndNotInTheFuture(decimal amount, int daysAhead, string message)
    {
        var invoice = IssuedInvoice(Item(1, 1000m));

        Should.Throw<DomainRuleException>(() => invoice.RecordPayment(amount, Today.AddDays(daysAhead), PaymentMethod.Cash, null, null, Today))
            .Message.ShouldContain(message);
        invoice.Payments.ShouldBeEmpty();
        invoice.AmountPaid.ShouldBe(0m);
    }

    [Fact]
    public void RemovingAPayment_PutsTheBalanceBack()
    {
        var invoice = IssuedInvoice(Item(1, 1000m));
        var payment = Pay(invoice, 1000m);

        invoice.RemovePayment(payment.Id);

        invoice.Payments.ShouldBeEmpty();
        invoice.BalanceDue.ShouldBe(1000m);
        invoice.GetStatus(Today).ShouldBe(InvoiceStatus.Unpaid);
        Should.Throw<DomainRuleException>(() => invoice.RemovePayment(payment.Id));
    }

    [Fact]
    public void VoidInvoices_KeepTheirPayments_ButTheyCanNoLongerChange()
    {
        var invoice = IssuedInvoice(Item(1, 1000m));
        var payment = Pay(invoice, 300m);

        invoice.Void(Now, "Duplicate");

        invoice.Payments.ShouldHaveSingleItem();
        Should.Throw<DomainRuleException>(() => invoice.RemovePayment(payment.Id));
        Should.Throw<DomainRuleException>(() => Pay(invoice, 100m));
    }

    [Fact]
    public void ACreditNote_ReducesTheBalance_AndSplitsOutItsTax()
    {
        var invoice = TaxedInvoice(); // 1000 + 20% tax = 1200

        var credit = invoice.IssueCreditNote("CN-0001", 300m, Today, " Returned one licence ", Today);

        credit.Number.ShouldBe("CN-0001");
        credit.Reason.ShouldBe("Returned one licence");
        credit.Amount.ShouldBe(300m);
        credit.TaxAmount.ShouldBe(50m);
        credit.NetAmount.ShouldBe(250m);
        invoice.CreditedAmount.ShouldBe(300m);
        invoice.BalanceDue.ShouldBe(900m);
        invoice.GetStatus(Today).ShouldBe(InvoiceStatus.Unpaid);
    }

    [Fact]
    public void CreditingEverythingLeft_IsCredited_AndReversesExactlyTheInvoicesTax()
    {
        var invoice = TaxedInvoice();
        invoice.IssueCreditNote("CN-0001", 100.01m, Today, "Discount", Today); // tax 16.67 after rounding
        invoice.IssueCreditNote("CN-0002", 100.01m, Today, "Discount", Today);

        var last = invoice.IssueCreditNote("CN-0003", invoice.BalanceDue, Today, "Cancelled", Today);

        invoice.BalanceDue.ShouldBe(0m);
        invoice.GetStatus(Today).ShouldBe(InvoiceStatus.Credited);
        invoice.CreditNotes.Sum(c => c.TaxAmount).ShouldBe(invoice.TaxAmount);
        last.Amount.ShouldBe(999.98m);
    }

    [Fact]
    public void PaidPartlyByPaymentAndPartlyByCredit_IsPaid()
    {
        var invoice = TaxedInvoice();
        Pay(invoice, 1000m);

        invoice.IssueCreditNote("CN-0001", 200m, Today, "Goodwill", Today);

        invoice.GetStatus(Today).ShouldBe(InvoiceStatus.Paid);
        Should.Throw<DomainRuleException>(() => Pay(invoice, 1m)).Message.ShouldContain("still due");
    }

    [Theory]
    [InlineData(0, 0, "Reason", "more than zero")]
    [InlineData(1200.01, 0, "Reason", "more than the 1200.00 EUR still due")]
    [InlineData(100, 1, "Reason", "future")]
    [InlineData(100, 0, " ", "needs a reason")]
    public void CreditNotes_MustBePositive_NotMoreThanIsDue_NotInTheFuture_AndExplained(
        decimal amount, int daysAhead, string reason, string message)
    {
        var invoice = TaxedInvoice();

        Should.Throw<DomainRuleException>(() => invoice.IssueCreditNote("CN-0001", amount, Today.AddDays(daysAhead), reason, Today))
            .Message.ShouldContain(message);
        invoice.CreditNotes.ShouldBeEmpty();
    }

    [Fact]
    public void CreditNotes_AreOnlyForIssuedInvoices_AndStopTheInvoiceBeingVoided()
    {
        var draft = CreateInvoice(Item(1, 100m));
        Should.Throw<DomainRuleException>(() => draft.IssueCreditNote("CN-0001", 10m, Today, "Reason", Today));

        var invoice = TaxedInvoice();
        invoice.IssueCreditNote("CN-0001", 10m, Today, "Reason", Today);

        Should.Throw<DomainRuleException>(() => invoice.Void(Now, "Duplicate")).Message.ShouldContain("has credit notes");
        invoice.State.ShouldBe(InvoiceState.Issued);
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
            From = "Acme Ltd",
            To = "Globex Corporation",
            IssueDate = Today.AddDays(-14)
        };
        invoice.ReplaceItems(items);
        return invoice;
    }

    /// <summary>1000.00 EUR plus 20% tax: 1200.00 EUR, issued.</summary>
    private static Invoice TaxedInvoice()
    {
        var invoice = IssuedInvoice(Item(1, 1000m));
        invoice.Tax = 20;
        invoice.TaxType = TaxType.Percentage;
        return invoice;
    }

    private static Invoice IssuedInvoice(params InvoiceItem[] items)
    {
        var invoice = CreateInvoice(items);
        invoice.Currency = "EUR";
        invoice.Issue(Now, "INV-001");
        return invoice;
    }

    private static Payment Pay(Invoice invoice, decimal amount) =>
        invoice.RecordPayment(amount, Today, PaymentMethod.BankTransfer, null, null, Today);

    private static InvoiceItem Item(decimal quantity, decimal unitPrice) =>
        new() { Description = "Work", Quantity = quantity, UnitPrice = unitPrice };
}
