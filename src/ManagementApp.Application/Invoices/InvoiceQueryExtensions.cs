using FluentValidation;
using FluentValidation.Results;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices;

internal static class InvoiceQueryExtensions
{
    public static IQueryable<Invoice> OwnedBy(this IQueryable<Invoice> invoices, string userId) =>
        invoices.Where(i => i.OwnerId == userId);

    public static async Task<Invoice> GetOwnedAsync(
        this IQueryable<Invoice> invoices, int id, string userId, CancellationToken cancellationToken) =>
        await invoices.OwnedBy(userId).FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), id);

    public static async Task EnsureInvoiceNumberIsUniqueAsync(
        this IQueryable<Invoice> invoices, string invoiceNumber, string userId, int? excludeId, CancellationToken cancellationToken)
    {
        var number = invoiceNumber.Trim();
        var taken = await invoices
            .OwnedBy(userId)
            .AnyAsync(i => i.InvoiceNumber == number && i.Id != excludeId, cancellationToken);

        if (taken)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(SaveInvoiceCommand.InvoiceNumber), $"Invoice number '{number}' is already in use.")
            ]);
        }
    }
}
