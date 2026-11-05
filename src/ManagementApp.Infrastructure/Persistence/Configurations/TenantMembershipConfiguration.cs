using ManagementApp.Domain.Tenancy;
using ManagementApp.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApp.Infrastructure.Persistence.Configurations;

public class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);
    }
}

public class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
{
    public void Configure(EntityTypeBuilder<TenantInvitation> builder)
    {
        builder.Property(i => i.Email).HasMaxLength(256).IsRequired();
        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(i => i.AcceptedByUserId).HasMaxLength(450);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.Email });
    }
}

public class TenantLogoConfiguration : IEntityTypeConfiguration<TenantLogo>
{
    public void Configure(EntityTypeBuilder<TenantLogo> builder)
    {
        builder.HasKey(l => l.TenantId);
        builder.Property(l => l.ContentType).HasMaxLength(50).IsRequired();
        builder.Property(l => l.Content).HasMaxLength(TenantLogo.MaxBytes).IsRequired();

        builder.HasOne<Tenant>().WithOne().HasForeignKey<TenantLogo>(l => l.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
