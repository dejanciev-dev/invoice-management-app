using ManagementApp.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApp.Infrastructure.Persistence.Configurations;

/// <summary>Audit records have no foreign keys on purpose: they must outlive the tenants and users they mention.</summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(a => a.UserId).HasMaxLength(450);
        builder.Property(a => a.ImpersonatorUserId).HasMaxLength(450);
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(100);
        builder.Property(a => a.IpAddress).HasMaxLength(45);

        builder.HasIndex(a => new { a.TenantId, a.Timestamp });
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.Timestamp);
    }
}

public class ImpersonationSessionConfiguration : IEntityTypeConfiguration<ImpersonationSession>
{
    public void Configure(EntityTypeBuilder<ImpersonationSession> builder)
    {
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.ImpersonatorUserId).HasMaxLength(450).IsRequired();
        builder.Property(s => s.TargetUserId).HasMaxLength(450).IsRequired();
        builder.Property(s => s.Reason).HasMaxLength(500);

        builder.HasIndex(s => s.ImpersonatorUserId);
        builder.HasIndex(s => s.TenantId);
    }
}
