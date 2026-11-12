using ManagementApp.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace ManagementApp.Infrastructure.Files.Storage;

/// <summary>
/// Keeps files on the local disk under <see cref="FileStorageOptions.LocalRootPath"/>, for development, a single
/// server or a container with a mounted volume. Each file's content type is kept next to it in a <c>.type</c> file.
/// </summary>
public sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private const string ContentTypeSuffix = ".type";

    private readonly string _root = Path.GetFullPath(options.Value.LocalRootPath);

    public async Task SaveAsync(Guid tenantId, string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var file = PathFor(FileStorageKey.For(tenantId, path));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        // Written to a temporary file first, so readers never see a half-written file.
        var temporary = $"{file}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var target = File.Create(temporary))
            {
                await content.CopyToAsync(target, cancellationToken);
            }

            await File.WriteAllTextAsync(file + ContentTypeSuffix, contentType, cancellationToken);
            File.Move(temporary, file, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public Task<StoredFile?> OpenReadAsync(Guid tenantId, string path, CancellationToken cancellationToken)
    {
        var file = PathFor(FileStorageKey.For(tenantId, path));
        if (!File.Exists(file))
        {
            return Task.FromResult<StoredFile?>(null);
        }

        var contentType = File.Exists(file + ContentTypeSuffix)
            ? File.ReadAllText(file + ContentTypeSuffix)
            : "application/octet-stream";
        var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, useAsync: true);
        return Task.FromResult<StoredFile?>(new StoredFile(stream, contentType, stream.Length));
    }

    public Task<bool> DeleteAsync(Guid tenantId, string path, CancellationToken cancellationToken)
    {
        var file = PathFor(FileStorageKey.For(tenantId, path));
        if (!File.Exists(file))
        {
            return Task.FromResult(false); // File.Delete would throw if the folder does not exist either
        }

        File.Delete(file);
        File.Delete(file + ContentTypeSuffix);
        return Task.FromResult(true);
    }

    public Task DeleteAllAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var directory = PathFor(FileStorageKey.TenantPrefix(tenantId));
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>The key's place under the root. Keys are already validated; this is a second check against escaping it.</summary>
    private string PathFor(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to touch a file outside the storage root.");
        }

        return full;
    }
}
