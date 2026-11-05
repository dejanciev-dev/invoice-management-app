using FluentValidation;
using ManagementApp.Application.Common.Exceptions;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Tenants;

public sealed class GetCurrentTenantHandler(IApplicationDbContext context, ITenantContext tenantContext, TimeProvider timeProvider)
{
    public async Task<CurrentTenantDto> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequireTenantId();
        var tenant = await context.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        return tenant.ToCurrentTenantDto(timeProvider.GetUtcNow());
    }
}

/// <summary>
/// Updates a tenant's settings. Tenant admins can only reach it for their own tenant (the endpoint passes the
/// server-resolved tenant); platform admins pass the tenant they are managing.
/// </summary>
public sealed class UpdateTenantSettingsHandler(
    IApplicationDbContext context,
    IAuditLogger auditLogger,
    IValidator<TenantSettingsDto> validator,
    TimeProvider timeProvider)
{
    public async Task<CurrentTenantDto> HandleAsync(Guid tenantId, TenantSettingsDto settings, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(settings, cancellationToken);

        var tenant = await context.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        var brandingChanged = settings.Invoice.BrandColor != tenant.InvoiceSettings.BrandColor;
        if (brandingChanged && !string.IsNullOrWhiteSpace(settings.Invoice.BrandColor)
            && !tenant.Subscription.HasFeature(PlanFeature.CustomBranding))
        {
            throw new ForbiddenException("Your plan does not include custom branding.");
        }

        tenant.ApplySettings(settings);
        auditLogger.Add(new AuditEntry(AuditActions.TenantSettingsUpdated)
        {
            TenantId = tenant.Id,
            EntityType = nameof(Tenant),
            EntityId = tenant.Id.ToString(),
            Details = new { tenant.Name }
        });

        await context.SaveChangesAsync(cancellationToken);
        return tenant.ToCurrentTenantDto(timeProvider.GetUtcNow());
    }
}

public sealed record LogoFile(string ContentType, byte[] Content);

public sealed class TenantLogoHandler(
    IApplicationDbContext context, ITenantContext tenantContext, IAuditLogger auditLogger, TimeProvider timeProvider)
{
    /// <summary>Reads a tenant's logo. Tenant users pass their own tenant; the query filter blocks any other.</summary>
    public async Task<LogoFile?> GetAsync(Guid tenantId, bool ignoreTenantFilter, CancellationToken cancellationToken)
    {
        var logos = ignoreTenantFilter ? context.TenantLogos.IgnoreQueryFilters() : context.TenantLogos;
        return await logos.AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .Select(l => new LogoFile(l.ContentType, l.Content))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>Replaces the current tenant's logo.</summary>
    public async Task UploadAsync(LogoFile file, CancellationToken cancellationToken)
    {
        if (!TenantLogo.AllowedContentTypes.Contains(file.ContentType))
        {
            throw new ValidationException([new FluentValidation.Results.ValidationFailure("file", "Upload a PNG, JPEG or WebP image.")]);
        }

        if (file.Content.Length is 0 or > TenantLogo.MaxBytes)
        {
            throw new ValidationException([new FluentValidation.Results.ValidationFailure("file", "The logo must be smaller than 512 KB.")]);
        }

        if (!LooksLikeImage(file.Content))
        {
            throw new ValidationException([new FluentValidation.Results.ValidationFailure("file", "The file is not a valid image.")]);
        }

        var tenantId = tenantContext.RequireTenantId();
        var tenant = await context.Tenants.SingleAsync(t => t.Id == tenantId, cancellationToken);
        var logo = await context.TenantLogos.SingleOrDefaultAsync(l => l.TenantId == tenantId, cancellationToken);
        if (logo is null)
        {
            context.TenantLogos.Add(new TenantLogo
            {
                TenantId = tenantId,
                ContentType = file.ContentType.ToLowerInvariant(),
                Content = file.Content,
                UploadedAt = timeProvider.GetUtcNow()
            });
        }
        else
        {
            logo.ContentType = file.ContentType.ToLowerInvariant();
            logo.Content = file.Content;
            logo.UploadedAt = timeProvider.GetUtcNow();
        }

        tenant.HasLogo = true;
        auditLogger.Add(new AuditEntry(AuditActions.TenantLogoChanged) { TenantId = tenantId, EntityType = nameof(Tenant), EntityId = tenantId.ToString() });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequireTenantId();
        var tenant = await context.Tenants.SingleAsync(t => t.Id == tenantId, cancellationToken);
        var logo = await context.TenantLogos.SingleOrDefaultAsync(l => l.TenantId == tenantId, cancellationToken);
        if (logo is not null)
        {
            context.TenantLogos.Remove(logo);
        }

        tenant.HasLogo = false;
        auditLogger.Add(new AuditEntry(AuditActions.TenantLogoChanged)
        {
            TenantId = tenantId, EntityType = nameof(Tenant), EntityId = tenantId.ToString(), Details = new { Removed = true }
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    // Checks the magic bytes, so a renamed HTML or SVG file cannot be served back as an image.
    private static bool LooksLikeImage(byte[] content) =>
        content.AsSpan().StartsWith((byte[])[0x89, 0x50, 0x4E, 0x47])                       // PNG
        || content.AsSpan().StartsWith((byte[])[0xFF, 0xD8, 0xFF])                          // JPEG
        || (content.Length > 12 && content.AsSpan(0, 4).SequenceEqual("RIFF"u8) && content.AsSpan(8, 4).SequenceEqual("WEBP"u8));
}
