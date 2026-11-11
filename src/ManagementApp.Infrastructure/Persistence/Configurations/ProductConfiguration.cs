using ManagementApp.Application.Common;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApp.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(500).IsRequired(); // as long as an invoice line's description
        builder.Property(p => p.Unit).HasMaxLength(20);
        builder.Property(p => p.UnitPrice).HasPrecision(18, 2);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.TenantId, p.Name });

        builder.HasQueryFilter(QueryFilters.NotDeleted, p => p.DeletedAt == null);
    }
}
