using ManagementApp.Domain.Messaging;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApp.Infrastructure.Persistence.Configurations;

public class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.Property(e => e.To).HasMaxLength(EmailMessage.AddressMaxLength).IsRequired();
        builder.Property(e => e.FromName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ReplyTo).HasMaxLength(EmailMessage.AddressMaxLength);
        builder.Property(e => e.Subject).HasMaxLength(EmailMessage.SubjectMaxLength).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.RelatedEntityType).HasMaxLength(50);
        builder.Property(e => e.RelatedEntityId).HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ProviderMessageId).HasMaxLength(200);
        builder.Property(e => e.LastError).HasMaxLength(EmailMessage.ErrorMaxLength);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Attachments)
            .WithOne()
            .HasForeignKey(a => a.EmailMessageId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Attachments).AutoInclude().UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => new { e.Status, e.NextAttemptAt }); // the worker's "what is due" query, across tenants
        builder.HasIndex(e => new { e.TenantId, e.CreatedAt });
        builder.HasIndex(e => new { e.TenantId, e.RelatedEntityType, e.RelatedEntityId });
    }
}

public class EmailAttachmentConfiguration : IEntityTypeConfiguration<EmailAttachment>
{
    public void Configure(EntityTypeBuilder<EmailAttachment> builder)
    {
        builder.ToTable("EmailAttachments");
        builder.Property(a => a.FileName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.StoragePath).HasMaxLength(400).IsRequired();
    }
}

public class SystemEmailMessageConfiguration : IEntityTypeConfiguration<SystemEmailMessage>
{
    public void Configure(EntityTypeBuilder<SystemEmailMessage> builder)
    {
        builder.Property(e => e.To).HasMaxLength(OutboxMessage.AddressMaxLength).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(OutboxMessage.SubjectMaxLength).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(450);
        builder.Property(e => e.Purpose).HasMaxLength(SystemEmailMessage.PurposeMaxLength).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ProviderMessageId).HasMaxLength(200);
        builder.Property(e => e.LastError).HasMaxLength(OutboxMessage.ErrorMaxLength);

        builder.HasIndex(e => new { e.Status, e.NextAttemptAt }); // the worker's "what is due" query
        builder.HasIndex(e => new { e.UserId, e.Purpose, e.CreatedAt });
    }
}
