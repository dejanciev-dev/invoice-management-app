using Azure.Storage.Blobs;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Infrastructure.Files.Storage;
using Microsoft.Extensions.Options;
using System.Text;

namespace ManagementApp.Infrastructure.IntegrationTests.Files.Storage;

/// <summary>What every <see cref="IFileStorage"/> must do, run against each implementation.</summary>
public abstract class FileStorageContractTests
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid Mine = Guid.NewGuid();
    private static readonly Guid Theirs = Guid.NewGuid();

    protected abstract IFileStorage Storage { get; }

    [Fact]
    public async Task SavedFiles_ReadBackWithTheirContentType()
    {
        await SaveAsync(Mine, "invoices/1/INV-0001.pdf", "%PDF-1.7 first", "application/pdf");

        await using var file = await Storage.OpenReadAsync(Mine, "invoices/1/INV-0001.pdf", Ct);

        file.ShouldNotBeNull();
        file.ContentType.ShouldBe("application/pdf");
        file.Length.ShouldBe(14);
        (await ReadAsync(file)).ShouldBe("%PDF-1.7 first");
    }

    [Fact]
    public async Task SavingAgain_ReplacesTheFile()
    {
        await SaveAsync(Mine, "replace.txt", "old", "text/plain");
        await SaveAsync(Mine, "replace.txt", "new and longer", "text/markdown");

        await using var file = await Storage.OpenReadAsync(Mine, "replace.txt", Ct);

        (await ReadAsync(file!)).ShouldBe("new and longer");
        file!.ContentType.ShouldBe("text/markdown");
    }

    [Fact]
    public async Task TheSamePath_InTwoCompanies_IsTwoDifferentFiles()
    {
        await SaveAsync(Mine, "same/path.txt", "mine", "text/plain");
        await SaveAsync(Theirs, "same/path.txt", "theirs", "text/plain");

        await using var mine = await Storage.OpenReadAsync(Mine, "same/path.txt", Ct);
        await using var theirs = await Storage.OpenReadAsync(Theirs, "same/path.txt", Ct);

        (await ReadAsync(mine!)).ShouldBe("mine");
        (await ReadAsync(theirs!)).ShouldBe("theirs");
    }

    [Fact]
    public async Task MissingFiles_AreNull_AndDeletingThemIsHarmless()
    {
        (await Storage.OpenReadAsync(Mine, "missing.pdf", Ct)).ShouldBeNull();
        (await Storage.DeleteAsync(Mine, "missing.pdf", Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task DeletedFiles_AreGone()
    {
        await SaveAsync(Mine, "delete/me.txt", "bye", "text/plain");

        (await Storage.DeleteAsync(Mine, "delete/me.txt", Ct)).ShouldBeTrue();

        (await Storage.OpenReadAsync(Mine, "delete/me.txt", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task DeletingAllOfACompanysFiles_LeavesOtherCompaniesAlone()
    {
        var company = Guid.NewGuid();
        await SaveAsync(company, "a.txt", "a", "text/plain");
        await SaveAsync(company, "nested/b.txt", "b", "text/plain");
        await SaveAsync(Theirs, "kept.txt", "kept", "text/plain");

        await Storage.DeleteAllAsync(company, Ct);
        await Storage.DeleteAllAsync(Guid.NewGuid(), Ct); // a company without files

        (await Storage.OpenReadAsync(company, "a.txt", Ct)).ShouldBeNull();
        (await Storage.OpenReadAsync(company, "nested/b.txt", Ct)).ShouldBeNull();
        await using var kept = await Storage.OpenReadAsync(Theirs, "kept.txt", Ct);
        kept.ShouldNotBeNull();
    }

    [Fact]
    public async Task UnsafePaths_AreRefused()
    {
        await Should.ThrowAsync<ArgumentException>(() => SaveAsync(Mine, "../escape.txt", "x", "text/plain"));
        await Should.ThrowAsync<ArgumentException>(() => Storage.OpenReadAsync(Mine, $"../{Theirs}/kept.txt", Ct));
    }

    private Task SaveAsync(Guid tenantId, string path, string text, string contentType) =>
        Storage.SaveAsync(tenantId, path, new MemoryStream(Encoding.UTF8.GetBytes(text)), contentType, Ct);

    private static async Task<string> ReadAsync(StoredFile file)
    {
        using var reader = new StreamReader(file.Content);
        return await reader.ReadToEndAsync(Ct);
    }
}

public sealed class LocalFileStorageTests : FileStorageContractTests, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "file-storage-tests", Guid.NewGuid().ToString("N"));

    public LocalFileStorageTests() =>
        Storage = new LocalFileStorage(Options.Create(new FileStorageOptions { LocalRootPath = _root }));

    protected override IFileStorage Storage { get; }

    [Fact]
    public async Task FilesAreKeptUnderTheTenantsFolder()
    {
        var tenantId = Guid.NewGuid();
        await Storage.SaveAsync(tenantId, "invoices/1.pdf", new MemoryStream([1, 2, 3]), "application/pdf", Ct);

        File.Exists(Path.Combine(_root, "tenants", tenantId.ToString(), "invoices", "1.pdf")).ShouldBeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

/// <summary>
/// Runs against a real Blob Storage endpoint (e.g. Azurite) when <c>FILESTORAGE_TEST_AZURE</c> holds a connection
/// string, and is skipped otherwise. Each test class instance uses its own container and removes it afterwards.
/// </summary>
public sealed class AzureBlobFileStorageTests : FileStorageContractTests, IAsyncLifetime
{
    private static readonly string? ConnectionString = Environment.GetEnvironmentVariable("FILESTORAGE_TEST_AZURE");
    private readonly BlobContainerClient? _container;

    public AzureBlobFileStorageTests()
    {
        if (ConnectionString is not null)
        {
            _container = new BlobContainerClient(ConnectionString, $"test-{Guid.NewGuid():N}");
        }

        Storage = _container is null ? null! : new AzureBlobFileStorage(_container);
    }

    protected override IFileStorage Storage { get; }

    public ValueTask InitializeAsync()
    {
        Assert.SkipWhen(ConnectionString is null,
            "Set FILESTORAGE_TEST_AZURE to a Blob Storage connection string (e.g. Azurite's) to run these tests.");
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DeleteIfExistsAsync();
        }
    }
}
