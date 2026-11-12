using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApp.Infrastructure.Persistence.Configurations;

public class InvoiceNumberSequenceConfiguration : IEntityTypeConfiguration<InvoiceNumberSequence>
{
    public void Configure(EntityTypeBuilder<InvoiceNumberSequence> builder)
    {
        // One row per company: the key is the tenant itself, so a second row cannot exist.
        builder.HasKey(s => s.TenantId);

        builder.HasOne<Tenant>()
            .WithOne()
            .HasForeignKey<InvoiceNumberSequence>(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
