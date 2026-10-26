using FluentValidation;
using ManagementApp.Application.Common.Interfaces;

namespace ManagementApp.Application.Invoices.Commands;

public sealed class UpdateInvoiceHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IValidator<SaveInvoiceCommand> validator,
    TimeProvider timeProvider)
{
    public async Task<InvoiceDto> HandleAsync(int id, SaveInvoiceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var userId = currentUser.RequireUserId();
        var invoice = await context.Invoices.GetOwnedAsync(id, userId, cancellationToken);
        await context.Invoices.EnsureInvoiceNumberIsUniqueAsync(command.InvoiceNumber, userId, excludeId: id, cancellationToken);

        command.ApplyTo(invoice);
        await context.SaveChangesAsync(cancellationToken);

        return invoice.ToDto(timeProvider.Today());
    }
}
