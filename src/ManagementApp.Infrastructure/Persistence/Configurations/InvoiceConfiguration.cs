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
        builder.Property(i => i.InvoiceNumber).HasMaxLength(Invoice.InvoiceNumberMaxLength); // null while a draft
        builder.Property(i => i.From).HasMaxLength(1000).IsRequired();
        builder.Property(i => i.To).HasMaxLength(1000).IsRequired();
        builder.Property(i => i.PaymentTerms).HasMaxLength(200);
        builder.Property(i => i.Notes).HasMaxLength(2000);
        builder.Property(i => i.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(i => i.State).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.VoidReason).HasMaxLength(Invoice.VoidReasonMaxLength);

        builder.Property(i => i.Discount).HasPrecision(18, 2);
        builder.Property(i => i.Tax).HasPrecision(18, 2);
        builder.Property(i => i.AmountPaid).HasPrecision(18, 2);
        builder.Property(i => i.CreditedAmount).HasPrecision(18, 2);
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

        // Customers are only soft deleted, so they never disappear from under an invoice.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Issued numbers are unique per company (drafts have none, and NULLs never collide); the index also serves
        // every tenant-filtered invoice query.
        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.IssueDate });
        builder.HasIndex(i => new { i.TenantId, i.State, i.DueDate }); // status filters, overdue and statistics
        builder.HasIndex(i => new { i.TenantId, i.CustomerId, i.IssueDate }); // a customer's invoices
        builder.HasIndex(i => i.OwnerId);

        builder.HasMany(i => i.Items)
            .WithOne()
            .HasForeignKey(i => i.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Items).AutoInclude();

        // Payments belong to the invoice and are only changed through it; loaded with it so AmountPaid can be recomputed.
        builder.HasMany(i => i.Payments)
            .WithOne()
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Payments).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(i => i.CreditNotes)
            .WithOne()
            .HasForeignKey(c => c.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade); // only drafts can be deleted, and drafts have no credit notes

        builder.Navigation(i => i.CreditNotes).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);

        // Moved on by ApplicationDbContext whenever the invoice row changes, so two people changing the same invoice at
        // once (e.g. recording payments) cannot overwrite each other: the second save fails instead.
        builder.Property<int>(ConcurrencyVersion).IsConcurrencyToken();
    }

    /// <summary>EF shadow property used as the invoice's optimistic concurrency token.</summary>
    public const string ConcurrencyVersion = "Version";
}
