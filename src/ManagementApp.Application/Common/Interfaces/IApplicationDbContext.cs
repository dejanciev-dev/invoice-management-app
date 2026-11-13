using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Messaging;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Common.Interfaces;

/// <summary>
/// Sets of <see cref="Domain.Common.ITenantEntity"/> entities are filtered to the current tenant. Only platform-admin
/// use cases and background jobs may call <c>IgnoreQueryFilters()</c>, and they must then filter by tenant themselves.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Invoice> Invoices { get; }

    /// <summary>Also hides deleted customers (see <see cref="Customer.DeletedAt"/>).</summary>
    DbSet<Customer> Customers { get; }

    /// <summary>Also hides deleted products (see <see cref="Product.DeletedAt"/>).</summary>
    DbSet<Product> Products { get; }

    /// <summary>One row per tenant; see <see cref="Invoices.InvoiceNumbering"/>.</summary>
    DbSet<InvoiceNumberSequence> InvoiceNumberSequences { get; }

    /// <summary>Read-only access for queries; payments are added and removed through their <see cref="Invoice"/>.</summary>
    DbSet<Payment> Payments { get; }

    /// <summary>Read-only access for queries; credit notes are issued through their <see cref="Invoice"/>.</summary>
    DbSet<CreditNote> CreditNotes { get; }

    DbSet<Tenant> Tenants { get; }

    DbSet<TenantMembership> TenantMemberships { get; }

    DbSet<TenantInvitation> TenantInvitations { get; }

    DbSet<TenantLogo> TenantLogos { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<ImpersonationSession> ImpersonationSessions { get; }

    /// <summary>The outbox; see <see cref="Emails.EmailOutbox"/>.</summary>
    DbSet<EmailMessage> EmailMessages { get; }

    /// <summary>Account emails, which belong to no tenant; see <see cref="Emails.SystemEmailOutbox"/>.</summary>
    DbSet<SystemEmailMessage> SystemEmailMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Runs <paramref name="operation"/> in a transaction that works with the configured retry strategy.</summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken);
}
