using FluentValidation;
using FluentValidation.Results;
using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Customers;
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

    /// <summary>
    /// A new customer must be a saved, not deleted, customer of this tenant. An invoice may keep the customer it already
    /// has even if that customer was deleted since, so editing an old invoice does not force a change of customer.
    /// </summary>
    public static async Task EnsureCustomerCanBeUsedAsync(
        this IQueryable<Customer> customers, int? customerId, int? currentCustomerId, CancellationToken cancellationToken)
    {
        if (customerId is { } id && id != currentCustomerId)
        {
            await customers.EnsureExistsAsync(id, nameof(SaveInvoiceCommand.CustomerId), cancellationToken);
        }
    }

    /// <summary>The invoice's customer, including a deleted one (its name still belongs to the invoice's history).</summary>
    public static async Task<InvoiceCustomerDto?> GetInvoiceCustomerAsync(
        this IQueryable<Customer> customers, int? customerId, CancellationToken cancellationToken) =>
        customerId is not { } id
            ? null
            : await customers.IncludingDeleted()
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new InvoiceCustomerDto(c.Id, c.Name, c.DeletedAt != null))
                .FirstOrDefaultAsync(cancellationToken);

    /// <summary>The full invoice as returned by the API, with its status as of the tenant's today.</summary>
    public static async Task<InvoiceDto> ToDtoAsync(
        this Invoice invoice, IApplicationDbContext context, TenantClock tenantClock, CancellationToken cancellationToken) =>
        invoice.ToDto(
            await tenantClock.TodayAsync(cancellationToken),
            await context.Customers.GetInvoiceCustomerAsync(invoice.CustomerId, cancellationToken));

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
