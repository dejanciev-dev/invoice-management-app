using ManagementApp.Domain.Entities;
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

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(i => i.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.OwnerId, i.InvoiceNumber }).IsUnique();

        builder.HasMany(i => i.Items)
            .WithOne()
            .HasForeignKey(i => i.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Items).AutoInclude();
    }
}
