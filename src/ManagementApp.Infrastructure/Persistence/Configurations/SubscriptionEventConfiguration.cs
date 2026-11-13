using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApp.Infrastructure.Persistence.Configurations;

/// <summary>Like audit records, subscription events have no foreign key: billing history outlives a deleted tenant.</summary>
public class SubscriptionEventConfiguration : IEntityTypeConfiguration<SubscriptionEvent>
{
    public void Configure(EntityTypeBuilder<SubscriptionEvent> builder)
    {
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ActorUserId).HasMaxLength(450);
        builder.Property(e => e.FromPlanCode).HasMaxLength(50);
        builder.Property(e => e.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ToPlanCode).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Note).HasMaxLength(SubscriptionEvent.NoteMaxLength);
        builder.Property(e => e.ProviderEventId).HasMaxLength(200);

        builder.HasIndex(e => new { e.TenantId, e.At });
        builder.HasIndex(e => e.ProviderEventId).IsUnique().HasFilter("[ProviderEventId] IS NOT NULL");
    }
}
