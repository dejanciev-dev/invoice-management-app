using Azure.Storage.Blobs;
using ManagementApp.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ManagementApp.Infrastructure.Files.Storage;

/// <summary>The <c>FileStorage</c> configuration section.</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary><c>Local</c> (the default) or <c>AzureBlob</c>.</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>For <c>Local</c>: the folder holding the files; relative paths are under the app's content root.</summary>
    public string LocalRootPath { get; set; } = "App_Data/files";

    /// <summary>For <c>AzureBlob</c>: a storage account connection string.</summary>
    public string? AzureConnectionString { get; set; }

    /// <summary>For <c>AzureBlob</c>: the container holding all tenants' files.</summary>
    public string AzureContainerName { get; set; } = "files";
}

public static class FileStorageRegistration
{
    public static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .PostConfigure<IHostEnvironment>((options, environment) =>
                options.LocalRootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.LocalRootPath)))
            .Validate(o => o.Provider is "Local" or "AzureBlob", "FileStorage:Provider must be Local or AzureBlob.")
            .Validate(o => o.Provider != "AzureBlob" || !string.IsNullOrWhiteSpace(o.AzureConnectionString),
                "FileStorage:AzureConnectionString is required for the AzureBlob provider.")
            .ValidateOnStart();

        services.AddSingleton<IFileStorage>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<FileStorageOptions>>();
            return options.Value.Provider == "AzureBlob"
                ? new AzureBlobFileStorage(new BlobContainerClient(options.Value.AzureConnectionString, options.Value.AzureContainerName))
                : new LocalFileStorage(options);
        });

        return services;
    }
}
