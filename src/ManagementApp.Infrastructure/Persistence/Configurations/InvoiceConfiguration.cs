using ManagementApp.Application.Invoices;
using ManagementApp.Domain.Entities;
using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApp.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.Property(i => i.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(i => i.From).HasMaxLength(1000).IsRequired();
        builder.Property(i => i.To).HasMaxLength(1000).IsRequired();
        builder.Property(i => i.PaymentTerms).HasMaxLength(200);
        builder.Property(i => i.Notes).HasMaxLength(2000);
        builder.Property(i => i.Currency).HasMaxLength(3).IsFixedLength().IsRequired();

        builder.Property(i => i.Discount).HasPrecision(18, 2);
        builder.Property(i => i.Tax).HasPrecision(18, 2);
        builder.Property(i => i.AmountPaid).HasPrecision(18, 2);
        builder.Property(i => i.DiscountType).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.TaxType).HasConversion<string>().HasMaxLength(20);

        // Stored copies of the calculated totals, refreshed by ApplicationDbContext on every save (see InvoiceTotalsQueries).
        builder.Property<decimal>(InvoiceTotalsQueries.StoredTotal).HasColumnName("Total").HasPrecision(18, 2);
        builder.Property<decimal>(InvoiceTotalsQueries.StoredBalanceDue).HasColumnName("BalanceDue").HasPrecision(18, 2);

        // Invoices belong to the company, so they are never deleted along with the user who created them.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(i => i.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Numbers are unique per company; the index also serves every tenant-filtered invoice query.
        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.IssueDate });
        builder.HasIndex(i => new { i.TenantId, i.DueDate }); // overdue filters and statistics
        builder.HasIndex(i => i.OwnerId);

        builder.HasMany(i => i.Items)
            .WithOne()
            .HasForeignKey(i => i.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Items).AutoInclude();
    }
}
