using ManagementApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApp.Infrastructure.Persistence.Configurations;

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.Property(i => i.Description).HasMaxLength(500).IsRequired();
        builder.Property(i => i.Quantity).HasPrecision(18, 4);
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
    }
}
