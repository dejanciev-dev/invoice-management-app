using FluentValidation;
using FluentValidation.Results;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Invoices.Commands;
using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Invoices;

/// <remarks>
/// Invoices are scoped to the current tenant by the DbContext's global query filter, so these helpers never see
/// another tenant's invoices. An invoice in another tenant is reported as not found, which does not reveal that it exists.
/// </remarks>
internal static class InvoiceQueryExtensions
{
    public static async Task<Invoice> GetAsync(
        this IQueryable<Invoice> invoices, int id, CancellationToken cancellationToken) =>
        await invoices.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Invoice), id);

    public static async Task EnsureInvoiceNumberIsUniqueAsync(
        this IQueryable<Invoice> invoices, string invoiceNumber, int? excludeId, CancellationToken cancellationToken)
    {
        var number = invoiceNumber.Trim();
        var taken = await invoices.AnyAsync(i => i.InvoiceNumber == number && i.Id != excludeId, cancellationToken);

        if (taken)
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(SaveInvoiceCommand.InvoiceNumber), $"Invoice number '{number}' is already in use.")
            ]);
        }
    }
}
