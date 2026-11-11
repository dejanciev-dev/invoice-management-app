using FluentValidation;
using FluentValidation.Results;
using ManagementApp.Application.Common;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Customers;

/// <remarks>
/// The query filters scope customers to the current tenant and hide deleted ones, so a customer of another tenant, or a
/// deleted one, is reported as not found. That does not reveal whether it exists.
/// </remarks>
public static class CustomerQueries
{
    /// <summary>Includes deleted customers, still only the current tenant's (the tenant filter stays on).</summary>
    public static IQueryable<Customer> IncludingDeleted(this IQueryable<Customer> customers) =>
        customers.IgnoreQueryFilters([QueryFilters.NotDeleted]);

    internal static async Task<Customer> GetAsync(this IQueryable<Customer> customers, int id, CancellationToken cancellationToken) =>
        await customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), id);

    /// <summary>Fails validation on <paramref name="field"/> unless the customer exists in this tenant and is not deleted.</summary>
    internal static async Task EnsureExistsAsync(
        this IQueryable<Customer> customers, int id, string field, CancellationToken cancellationToken)
    {
        if (!await customers.AnyAsync(c => c.Id == id, cancellationToken))
        {
            throw new ValidationException([new ValidationFailure(field, "Choose one of your saved customers.")]);
        }
    }
}
