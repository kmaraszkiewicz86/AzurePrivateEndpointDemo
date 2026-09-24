using Azure.Core;
using Azure.Identity;
using AzureAISearchIndexer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AzureAISearchIndexer.Services;

/// <summary>
/// Configures and creates the services required by AzureAISearchIndexerFunction.
/// </summary>
public static class FunctionServiceRegistration
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddIndexingServices()
        {
            builder.Services
                .AddOptions<AzureServicesOptions>()
                .BindConfiguration(AzureServicesOptions.SectionName)
                .ValidateDataAnnotations()
                .Validate(
                    options => Guid.TryParse(options.ManagedIdentityClientId, out _),
                    "Azure:ManagedIdentityClientId must be a valid client ID (GUID).")
                .Validate(
                    options => options.ChunkOverlapCharacters < options.MaxChunkCharacters,
                    "Azure:ChunkOverlapCharacters must be smaller than Azure:MaxChunkCharacters.")
                .ValidateOnStart();

            builder.Services.AddSingleton<TokenCredential>(provider =>
            {
                string clientId = provider.GetRequiredService<IOptions<AzureServicesOptions>>()
                    .Value.ManagedIdentityClientId;

                // Uses the selected user-assigned managed identity in Azure and developer credentials locally.
                return new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = clientId
                });
            });

            builder.Services.AddSingleton<AzureBlobService>();
            builder.Services.AddSingleton<DocumentIntelligenceService>();
            builder.Services.AddSingleton<AzureSearchService>();
            builder.Services.AddSingleton<ProcessedEventMemory>();

            return builder;
        }
    }
}
