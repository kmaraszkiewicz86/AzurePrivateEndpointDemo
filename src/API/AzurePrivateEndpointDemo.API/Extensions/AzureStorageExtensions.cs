using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using AzurePrivateEndpointDemo.API.Options;
using AzurePrivateEndpointDemo.API.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AzurePrivateEndpointDemo.API.Extensions;

/// <summary>
/// Registers Managed Identity Blob Storage access and document operations.
/// </summary>
public static class AzureStorageExtensions
{
    /// <summary>
    /// Adds the Blob Storage client and document service used by the API.
    /// </summary>
    /// <param name="builder">The .NET application host builder.</param>
    /// <returns>The same builder so registrations can be chained.</returns>
    public static IHostApplicationBuilder AddAzureStorage(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<AzureStorageOptions>()
            .BindConfiguration(AzureStorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // DefaultAzureCredential uses the developer identity locally and Managed Identity in Azure.
        builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        builder.Services.AddSingleton(serviceProvider =>
        {
            AzureStorageOptions options = serviceProvider
                .GetRequiredService<IOptions<AzureStorageOptions>>()
                .Value;
            TokenCredential credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new BlobServiceClient(new Uri(options.BlobServiceUri), credential)
                .GetBlobContainerClient(options.DocumentContainerName);
        });
        builder.Services.AddSingleton<DocumentService>();

        return builder;
    }
}
