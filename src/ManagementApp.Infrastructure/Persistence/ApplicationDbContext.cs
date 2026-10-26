using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Common;
using ManagementApp.Domain.Entities;
using ManagementApp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace ManagementApp.Infrastructure.Persistence;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
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

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
