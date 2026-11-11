using ManagementApp.Application.Customers;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApp.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Phone).HasMaxLength(50);
        builder.Property(c => c.Address).HasMaxLength(1000);
        builder.Property(c => c.TaxNumber).HasMaxLength(50);
        builder.Property(c => c.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // The list is sorted and searched by name within a company.
        builder.HasIndex(c => new { c.TenantId, c.Name });

        builder.HasQueryFilter(CustomerQueries.NotDeletedFilter, c => c.DeletedAt == null);
    }
}
