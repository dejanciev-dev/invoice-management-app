using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ManagementApp.Application.Common.Interfaces;

namespace ManagementApp.Infrastructure.Files.Storage;

/// <summary>
/// Keeps files in one Azure Blob Storage container, named by their tenant-prefixed key. The container is created on
/// first use and is never public: files are only served through the API, which checks the tenant.
/// </summary>
public sealed class AzureBlobFileStorage(BlobContainerClient container) : IFileStorage
{
    private int _containerReady;

    public async Task SaveAsync(Guid tenantId, string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken);
        var blob = container.GetBlobClient(FileStorageKey.For(tenantId, path));
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, cancellationToken);
    }

    public async Task<StoredFile?> OpenReadAsync(Guid tenantId, string path, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(FileStorageKey.For(tenantId, path));
        try
        {
            var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new StoredFile(download.Value.Content, download.Value.Details.ContentType, download.Value.Details.ContentLength);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null; // no such blob, or no container yet
        }
    }

    public async Task<bool> DeleteAsync(Guid tenantId, string path, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(FileStorageKey.For(tenantId, path));
        try
        {
            return (await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken)).Value;
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return false; // no container yet
        }
    }

    public async Task DeleteAllAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!(await container.ExistsAsync(cancellationToken)).Value)
        {
            return;
        }

        await foreach (var item in container.GetBlobsAsync(new GetBlobsOptions { Prefix = FileStorageKey.TenantPrefix(tenantId) }, cancellationToken))
        {
            await container.DeleteBlobIfExistsAsync(item.Name, cancellationToken: cancellationToken);
        }
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _containerReady) == 0)
        {
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            Volatile.Write(ref _containerReady, 1);
        }
    }
}
