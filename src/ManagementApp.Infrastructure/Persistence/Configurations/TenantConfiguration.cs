using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApp.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.RegistrationNumber).HasMaxLength(100);
        builder.Property(t => t.TaxNumber).HasMaxLength(100);
        builder.Property(t => t.Address).HasMaxLength(1000);
        builder.Property(t => t.Country).HasMaxLength(2).IsFixedLength();
        builder.Property(t => t.Email).HasMaxLength(256);
        builder.Property(t => t.Phone).HasMaxLength(50);
        builder.Property(t => t.DefaultCurrency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(t => t.DefaultLanguage).HasMaxLength(20).IsRequired();
        builder.Property(t => t.TimeZone).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.StatusReason).HasMaxLength(500);

        builder.HasIndex(t => t.Name);
        builder.HasIndex(t => t.Status);

        builder.OwnsOne(t => t.InvoiceSettings, settings =>
        {
            settings.Property(s => s.NumberPrefix).HasMaxLength(20).IsRequired();
            settings.Property(s => s.DefaultTaxRate).HasPrecision(5, 2);
            settings.Property(s => s.TaxLabel).HasMaxLength(20).IsRequired();
            settings.Property(s => s.DefaultNotes).HasMaxLength(2000);
            settings.Property(s => s.Footer).HasMaxLength(1000);
            settings.Property(s => s.BrandColor).HasMaxLength(7);
        });

        builder.OwnsOne(t => t.EmailSettings, settings =>
        {
            settings.Property(s => s.SenderName).HasMaxLength(100);
            settings.Property(s => s.ReplyToEmail).HasMaxLength(256);
            settings.Property(s => s.InvoiceEmailSubject).HasMaxLength(200);
            settings.Property(s => s.InvoiceEmailBody).HasMaxLength(4000);
        });

        builder.OwnsOne(t => t.NotificationSettings, settings =>
        {
            settings.Property(s => s.NotificationEmail).HasMaxLength(256);
        });

        builder.OwnsOne(t => t.Subscription, subscription =>
        {
            subscription.Property(s => s.PlanCode).HasMaxLength(50).IsRequired();
            subscription.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            subscription.Property(s => s.BillingCycle).HasConversion<string>().HasMaxLength(20);
        });

        builder.Navigation(t => t.InvoiceSettings).IsRequired();
        builder.Navigation(t => t.EmailSettings).IsRequired();
        builder.Navigation(t => t.NotificationSettings).IsRequired();
        builder.Navigation(t => t.Subscription).IsRequired();
    }
}
