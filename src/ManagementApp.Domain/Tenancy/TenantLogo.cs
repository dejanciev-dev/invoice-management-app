using ManagementApp.Domain.Common;

namespace ManagementApp.Domain.Tenancy;

/// <summary>The tenant's uploaded logo, stored in the database so it is isolated like any other tenant data.</summary>
public class TenantLogo : ITenantEntity
{
    public const int MaxBytes = 512 * 1024;

    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(["image/png", "image/jpeg", "image/webp"], StringComparer.OrdinalIgnoreCase);

    public Guid TenantId { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Content { get; set; }

    public DateTimeOffset UploadedAt { get; set; }
}
