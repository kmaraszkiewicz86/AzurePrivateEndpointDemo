using System.Collections.Concurrent;
using Azure.AI.DocumentIntelligence;
using Azure.Core;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
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
        /// <summary>
        /// Registers indexing services with the authentication mode selected in configuration.
        /// </summary>
        /// <returns>The same builder so registrations can be chained.</returns>
        public IHostApplicationBuilder AddIndexingServices()
        {
            builder.Services
                .AddOptions<AzureServicesOptions>()
                .BindConfiguration(AzureServicesOptions.SectionName)
                .ValidateDataAnnotations()
                .Validate(
                    options => !options.UseManagedIdentity || Guid.TryParse(options.ManagedIdentityClientId, out _),
                    "Azure:ManagedIdentityClientId must be a valid client ID (GUID) when Azure:UseManagedIdentity is true.")
                .Validate(
                    options => options.ChunkOverlapCharacters < options.MaxChunkCharacters,
                    "Azure:ChunkOverlapCharacters must be smaller than Azure:MaxChunkCharacters.")
                .ValidateOnStart();

            builder.Services.AddScoped<TokenCredential>(provider =>
            {
                AzureServicesOptions options = provider.GetRequiredService<IOptions<AzureServicesOptions>>().Value;

                if (options.UseManagedIdentity)
                {
                    return new ManagedIdentityCredential(
                        ManagedIdentityId.FromUserAssignedClientId(options.ManagedIdentityClientId));
                }

                // Local client-secret authentication uses AZURE_TENANT_ID, AZURE_CLIENT_ID,
                // and AZURE_CLIENT_SECRET; developer credentials are available when these are not configured.
                return new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ExcludeManagedIdentityCredential = true
                });
            });

            builder.Services.AddScoped<BlobContainerClient>(provider =>
            {
                AzureServicesOptions options = provider.GetRequiredService<IOptions<AzureServicesOptions>>().Value;
                TokenCredential credential = provider.GetRequiredService<TokenCredential>();

                return new BlobServiceClient(new Uri(options.BlobServiceUri), credential)
                    .GetBlobContainerClient(options.DocumentContainerName);
            });

            builder.Services.AddScoped<SearchIndexClient>(provider =>
            {
                AzureServicesOptions options = provider.GetRequiredService<IOptions<AzureServicesOptions>>().Value;
                return new SearchIndexClient(
                    new Uri(options.SearchEndpoint),
                    provider.GetRequiredService<TokenCredential>());
            });

            builder.Services.AddScoped<SearchClient>(provider =>
            {
                AzureServicesOptions options = provider.GetRequiredService<IOptions<AzureServicesOptions>>().Value;
                return provider.GetRequiredService<SearchIndexClient>().GetSearchClient(options.SearchIndexName);
            });

            builder.Services.AddScoped<DocumentIntelligenceClient>(provider =>
            {
                AzureServicesOptions options = provider.GetRequiredService<IOptions<AzureServicesOptions>>().Value;
                return new DocumentIntelligenceClient(
                    new Uri(options.DocumentIntelligenceEndpoint),
                    provider.GetRequiredService<TokenCredential>());
            });

            builder.Services.AddScoped<DocumentIntelligenceAdministrationClient>(provider =>
            {
                AzureServicesOptions options = provider.GetRequiredService<IOptions<AzureServicesOptions>>().Value;
                return new DocumentIntelligenceAdministrationClient(
                    new Uri(options.DocumentIntelligenceEndpoint),
                    provider.GetRequiredService<TokenCredential>());
            });

            builder.Services.AddScoped<IAzureBlobService, AzureBlobService>();
            builder.Services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();
            builder.Services.AddScoped<IAzureSearchService, AzureSearchService>();

            // Service instances are scoped to an invocation, but completed event IDs must
            // survive scope disposal so subsequent queue deliveries can still be recognized.
            var processedEventIds = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
            builder.Services.AddScoped<IProcessedEventMemory>(_ => new ProcessedEventMemory(processedEventIds));

            return builder;
        }
    }
}
