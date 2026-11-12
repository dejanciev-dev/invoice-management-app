using System.Text.RegularExpressions;

namespace ManagementApp.Application.Common.Interfaces;

/// <summary>
/// Stores files such as generated PDFs outside the database. Every file belongs to one company: callers pass the
/// tenant and a path within that tenant, and the storage key is always built by <see cref="FileStorageKey"/>, so code
/// cannot name a file of another company even by mistake.
/// </summary>
public interface IFileStorage
{
    /// <summary>Creates or replaces the file.</summary>
    Task SaveAsync(Guid tenantId, string path, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>The file, or null when there is none. The caller disposes the returned stream.</summary>
    Task<StoredFile?> OpenReadAsync(Guid tenantId, string path, CancellationToken cancellationToken);

    /// <summary>Deletes the file; returns false when there was none.</summary>
    Task<bool> DeleteAsync(Guid tenantId, string path, CancellationToken cancellationToken);

    /// <summary>Deletes every file of the company, e.g. when the company itself is deleted.</summary>
    Task DeleteAllAsync(Guid tenantId, CancellationToken cancellationToken);
}

public sealed record StoredFile(Stream Content, string ContentType, long Length) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>
/// Builds storage keys of the form <c>tenants/{tenantId}/{path}</c>. Paths are relative, made of segments of letters,
/// digits, dots, dashes and underscores separated by single slashes, so they cannot climb out of the tenant's prefix.
/// </summary>
public static partial class FileStorageKey
{
    public const int MaxPathLength = 400;

    public static string TenantPrefix(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Files must belong to a tenant.", nameof(tenantId));
        }

        return $"tenants/{tenantId:D}/";
    }

    public static string For(Guid tenantId, string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > MaxPathLength || !SafePath().IsMatch(path)
            || path.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException($"'{path}' is not a valid file path.", nameof(path));
        }

        return TenantPrefix(tenantId) + path;
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]+(/[A-Za-z0-9._-]+)*$")]
    private static partial Regex SafePath();
}
