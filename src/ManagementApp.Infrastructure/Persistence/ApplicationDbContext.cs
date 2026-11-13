using ManagementApp.Application.Invoices;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Common;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Messaging;
using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.BackgroundJobs;
using ManagementApp.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Reflection;

namespace ManagementApp.Infrastructure.Persistence;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext, IDataProtectionKeyContext
{
    public const string TenantFilter = "Tenant";

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<InvoiceNumberSequence> InvoiceNumberSequences => Set<InvoiceNumberSequence>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();

    public DbSet<TenantLogo> TenantLogos => Set<TenantLogo>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<ImpersonationSession> ImpersonationSessions => Set<ImpersonationSession>();

    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

    public DbSet<SystemEmailMessage> SystemEmailMessages => Set<SystemEmailMessage>();

    public DbSet<JobLease> JobLeases => Set<JobLease>();

    /// <summary>The keys that protect session cookies and tokens, shared by every instance of the app.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>Read by the tenant query filters on every query, so it always reflects the current scope.</summary>
    private Guid? CurrentTenantId => tenantContext.TenantId;

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceTenantBoundaries();
        StoreInvoiceTotals();
        BumpInvoiceVersions();
        SetAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantBoundaries();
        StoreInvoiceTotals();
        BumpInvoiceVersions();
        SetAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is not null)
        {
            await operation();
            return;
        }

        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            await operation();
            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <summary>
    /// The second line of defence behind the query filters: new tenant data is stamped with the current tenant, and
    /// data belonging to another tenant can never be written while a tenant context is active.
    /// </summary>
    private void EnforceTenantBoundaries()
    {
        var currentTenantId = CurrentTenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added when entry.Entity.TenantId == Guid.Empty:
                    entry.Entity.TenantId = currentTenantId
                        ?? throw new InvalidOperationException($"Cannot add a {entry.Metadata.ClrType.Name} without a tenant.");
                    break;

                case EntityState.Added when currentTenantId is { } tenantId && entry.Entity.TenantId != tenantId:
                    throw CrossTenantWrite(entry.Metadata.ClrType.Name);

                case EntityState.Modified or EntityState.Deleted:
                    if (entry.Property(e => e.TenantId).IsModified)
                    {
                        throw new InvalidOperationException($"A {entry.Metadata.ClrType.Name} cannot be moved to another tenant.");
                    }

                    if (currentTenantId is { } current && entry.Entity.TenantId != current)
                    {
                        throw CrossTenantWrite(entry.Metadata.ClrType.Name);
                    }

                    break;
            }
        }
    }

    private static InvalidOperationException CrossTenantWrite(string entity) =>
        new($"Refusing to write a {entity} that belongs to another tenant.");

    /// <summary>
    /// Copies each tracked invoice's calculated totals into its stored columns. Unchanged invoices are included too,
    /// because replacing line items changes the totals without touching the invoice row itself.
    /// </summary>
    private void StoreInvoiceTotals()
    {
        foreach (var entry in ChangeTracker.Entries<Invoice>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Unchanged))
        {
            Store(entry.Property<decimal>(InvoiceTotalsQueries.StoredTotal), entry.Entity.Total);
            Store(entry.Property<decimal>(InvoiceTotalsQueries.StoredBalanceDue), entry.Entity.BalanceDue);
        }

        static void Store(Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry<Invoice, decimal> property, decimal value)
        {
            if (property.CurrentValue != value)
            {
                property.CurrentValue = value;
            }
        }
    }

    /// <summary>
    /// Moves the concurrency version of every invoice whose row is being updated. The update only applies when the row
    /// still has the version that was read, so of two simultaneous changes the second fails with a concurrency error.
    /// </summary>
    private void BumpInvoiceVersions()
    {
        foreach (var entry in ChangeTracker.Entries<Invoice>().Where(e => e.State == EntityState.Modified))
        {
            var version = entry.Property<int>(Configurations.InvoiceConfiguration.ConcurrencyVersion);
            version.CurrentValue = version.OriginalValue + 1;
        }
    }

    private void SetAuditFields()
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = currentUserService.UserId;
                    entry.Entity.Created = timeProvider.GetUtcNow();
                    break;

                case EntityState.Modified:
                    entry.Entity.LastModifiedBy = currentUserService.UserId;
                    entry.Entity.LastModified = timeProvider.GetUtcNow();
                    break;
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite (used by the tests) cannot compare or sort DateTimeOffset values, so they are stored as numbers there.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
            configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Every tenant-owned entity gets the same filter, so a new entity cannot be forgotten.
        var applyFilter = typeof(ApplicationDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType) && t.BaseType is null && !t.IsOwned()))
        {
            applyFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantEntity =>
        builder.Entity<TEntity>().HasQueryFilter(TenantFilter, e => e.TenantId == CurrentTenantId);
}
