using FluentValidation;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Entities;

namespace ManagementApp.Application.Invoices.Commands;

public sealed class CreateInvoiceHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IValidator<SaveInvoiceCommand> validator,
    TimeProvider timeProvider)
{
    public async Task<InvoiceDto> HandleAsync(SaveInvoiceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var userId = currentUser.RequireUserId();
        await context.Invoices.EnsureInvoiceNumberIsUniqueAsync(command.InvoiceNumber, userId, excludeId: null, cancellationToken);

        var invoice = new Invoice
        {
            OwnerId = userId,
            InvoiceNumber = command.InvoiceNumber,
            From = command.From,
            To = command.To
        };
        command.ApplyTo(invoice);

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(cancellationToken);

        return invoice.ToDto(timeProvider.Today());
    }
}
