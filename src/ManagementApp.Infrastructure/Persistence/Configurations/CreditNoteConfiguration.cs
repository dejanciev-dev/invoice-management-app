using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApp.Infrastructure.Persistence.Configurations;

public class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.Property(c => c.Number).HasMaxLength(CreditNote.NumberMaxLength).IsRequired();
        builder.Property(c => c.Reason).HasMaxLength(CreditNote.ReasonMaxLength).IsRequired();
        builder.Property(c => c.Amount).HasPrecision(18, 2);
        builder.Property(c => c.TaxAmount).HasPrecision(18, 2);
        builder.Ignore(c => c.NetAmount);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Numbers are unique per company, like invoice numbers.
        builder.HasIndex(c => new { c.TenantId, c.Number }).IsUnique();
        builder.HasIndex(c => new { c.TenantId, c.Date });
    }
}
