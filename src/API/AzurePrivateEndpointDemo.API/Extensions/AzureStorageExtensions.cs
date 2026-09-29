using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using AzurePrivateEndpointDemo.API.Options;
using AzurePrivateEndpointDemo.API.Services;
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

        builder.Services
            .AddOptions<AzureIdentityOptions>()
            .BindConfiguration(AzureIdentityOptions.SectionName)
            .Validate(
                options => string.IsNullOrWhiteSpace(options.ManagedIdentityClientId)
                    || Guid.TryParse(options.ManagedIdentityClientId, out _),
                $"{AzureIdentityOptions.SectionName}:{nameof(AzureIdentityOptions.ManagedIdentityClientId)} must be a valid GUID when set.")
            .ValidateOnStart();

        // DefaultAzureCredential uses the developer identity locally and Managed Identity in Azure.
        // A configured client ID selects a user-assigned identity; an empty value keeps the system-assigned one.
        builder.Services.AddSingleton<TokenCredential>(serviceProvider =>
        {
            string? clientId = serviceProvider
                .GetRequiredService<IOptions<AzureIdentityOptions>>()
                .Value
                .ManagedIdentityClientId;

            return string.IsNullOrWhiteSpace(clientId)
                ? new DefaultAzureCredential()
                : new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId });
        });
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
