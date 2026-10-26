using FluentValidation.TestHelper;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Domain.Enums;

namespace ManagementApp.Application.UnitTests.Invoices;

public class SaveInvoiceCommandValidatorTests
{
    private readonly SaveInvoiceCommandValidator _validator = new();

    private static readonly SaveInvoiceCommand ValidCommand = new()
    {
        InvoiceNumber = "INV-001",
        From = "Acme Ltd",
        To = "Globex Corporation",
        IssueDate = new DateOnly(2026, 10, 1),
        DueDate = new DateOnly(2026, 10, 31),
        Currency = "EUR",
        Items = [new InvoiceItemInput("Consulting", 2, 150)]
    };

    [Fact]
    public void ValidCommand_HasNoErrors()
    {
        _validator.TestValidate(ValidCommand).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RequiresAtLeastOneItem()
    {
        var result = _validator.TestValidate(ValidCommand with { Items = [] });

        result.ShouldHaveValidationErrorFor(c => c.Items);
    }

    [Theory]
    [InlineData("", 1, 10, "Items[0].Description")]
    [InlineData("Work", 0, 10, "Items[0].Quantity")]
    [InlineData("Work", 1, -1, "Items[0].UnitPrice")]
    public void ValidatesEachItem(string description, decimal quantity, decimal unitPrice, string expectedProperty)
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Items = [new InvoiceItemInput(description, quantity, unitPrice)]
        });

        result.ShouldHaveValidationErrorFor(expectedProperty);
    }

    [Fact]
    public void RejectsDueDateBeforeIssueDate()
    {
        var result = _validator.TestValidate(ValidCommand with { DueDate = new DateOnly(2026, 9, 30) });

        result.ShouldHaveValidationErrorFor(c => c.DueDate)
            .WithErrorMessage("Due date cannot be before the issue date.");
    }

    [Fact]
    public void AllowsMissingDueDate()
    {
        _validator.TestValidate(ValidCommand with { DueDate = null }).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("usd")]
    [InlineData("EURO")]
    [InlineData("")]
    public void RejectsInvalidCurrencyCodes(string currency)
    {
        _validator.TestValidate(ValidCommand with { Currency = currency })
            .ShouldHaveValidationErrorFor(c => c.Currency);
    }

    [Fact]
    public void RejectsPercentagesAboveOneHundred()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Discount = 101,
            DiscountType = DiscountType.Percentage,
            Tax = 150,
            TaxType = TaxType.Percentage
        });

        result.ShouldHaveValidationErrorFor(c => c.Discount);
        result.ShouldHaveValidationErrorFor(c => c.Tax);
    }

    [Fact]
    public void AllowsFlatAmountsAboveOneHundred()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Discount = 250,
            DiscountType = DiscountType.Flat,
            Tax = 180,
            TaxType = TaxType.Flat
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RejectsNegativeAmounts()
    {
        var result = _validator.TestValidate(ValidCommand with { Discount = -1, Tax = -1, AmountPaid = -1 });

        result.ShouldHaveValidationErrorFor(c => c.Discount);
        result.ShouldHaveValidationErrorFor(c => c.Tax);
        result.ShouldHaveValidationErrorFor(c => c.AmountPaid);
    }

    [Fact]
    public void RequiresPartiesAndInvoiceNumber()
    {
        var result = _validator.TestValidate(ValidCommand with { InvoiceNumber = "", From = "", To = "" });

        result.ShouldHaveValidationErrorFor(c => c.InvoiceNumber);
        result.ShouldHaveValidationErrorFor(c => c.From);
        result.ShouldHaveValidationErrorFor(c => c.To);
    }
}
