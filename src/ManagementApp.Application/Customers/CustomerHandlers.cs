using FluentValidation;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Common.Subscriptions;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Customers;

public sealed record GetCustomersQuery(string? Search = null, int Page = 1, int PageSize = 25)
{
    public const int MaxPageSize = 100;
}

/// <summary>A page of the current tenant's customers, sorted by name. Search matches name, email or tax number.</summary>
public sealed class GetCustomersHandler(IApplicationDbContext context)
{
    public async Task<PagedResult<CustomerDto>> HandleAsync(GetCustomersQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, GetCustomersQuery.MaxPageSize);

        // Scoped to the current tenant, and without deleted customers, by the global query filters.
        IQueryable<Customer> customers = context.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            customers = customers.Where(c =>
                c.Name.Contains(search) || (c.Email != null && c.Email.Contains(search)) || (c.TaxNumber != null && c.TaxNumber.Contains(search)));
        }

        var total = await customers.CountAsync(cancellationToken);
        var items = await customers
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDto>(items.Select(c => c.ToDto()).ToList(), total, page, pageSize);
    }
}

public sealed class GetCustomerHandler(IApplicationDbContext context)
{
    public async Task<CustomerDto> HandleAsync(int id, CancellationToken cancellationToken) =>
        (await context.Customers.AsNoTracking().GetAsync(id, cancellationToken)).ToDto();
}

public sealed class CreateCustomerHandler(
    IApplicationDbContext context,
    ITenantContext tenantContext,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    IValidator<SaveCustomerCommand> validator)
{
    public async Task<CustomerDto> HandleAsync(SaveCustomerCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        var customer = new Customer { TenantId = tenantContext.RequireTenantId(), Name = command.Name };
        command.ApplyTo(customer);
        context.Customers.Add(customer);
        await context.SaveChangesAsync(cancellationToken);

        // Written after the save, because the id only exists once the customer is stored.
        await auditLogger.WriteAsync(new AuditEntry(AuditActions.CustomerCreated)
        {
            EntityType = nameof(Customer),
            EntityId = customer.Id.ToString(),
            Details = new { customer.Name }
        }, cancellationToken);

        return customer.ToDto();
    }
}

public sealed class UpdateCustomerHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    IValidator<SaveCustomerCommand> validator)
{
    public async Task<CustomerDto> HandleAsync(int id, SaveCustomerCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var customer = await context.Customers.GetAsync(id, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        var previousName = customer.Name;
        command.ApplyTo(customer);

        auditLogger.Add(new AuditEntry(AuditActions.CustomerUpdated)
        {
            EntityType = nameof(Customer),
            EntityId = customer.Id.ToString(),
            Details = new { customer.Name, PreviousName = previousName == customer.Name ? null : previousName }
        });

        await context.SaveChangesAsync(cancellationToken);
        return customer.ToDto();
    }
}

/// <summary>Soft deletes a customer: it disappears from the app but its row stays for the invoices' history.</summary>
public sealed class DeleteCustomerHandler(
    IApplicationDbContext context,
    SubscriptionGuard subscriptionGuard,
    IAuditLogger auditLogger,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(int id, CancellationToken cancellationToken)
    {
        var customer = await context.Customers.GetAsync(id, cancellationToken);
        await subscriptionGuard.EnsureCanChangeDataAsync(cancellationToken);

        customer.DeletedAt = timeProvider.GetUtcNow();
        auditLogger.Add(new AuditEntry(AuditActions.CustomerDeleted)
        {
            EntityType = nameof(Customer),
            EntityId = customer.Id.ToString(),
            Details = new { customer.Name }
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}

/// <remarks>
/// The query filters scope customers to the current tenant and hide deleted ones, so a customer of another tenant, or a
/// deleted one, is reported as not found. That does not reveal whether it exists.
/// </remarks>
internal static class CustomerQueryExtensions
{
    public static async Task<Customer> GetAsync(this IQueryable<Customer> customers, int id, CancellationToken cancellationToken) =>
        await customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), id);
}
