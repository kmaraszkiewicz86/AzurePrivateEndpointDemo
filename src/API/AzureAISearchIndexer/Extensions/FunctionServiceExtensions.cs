using Azure.Core;
using Azure.Identity;
using AzureAISearchIndexer.Options;
using AzureAISearchIndexer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AzureAISearchIndexer.Extensions;

/// <summary>
/// Registers the indexing services used by the .NET 10 isolated Function App.
/// </summary>
public static class FunctionServiceExtensions
{
    /// <summary>
    /// Adds configuration, Managed Identity credentials, and indexing services to the Function host.
    /// </summary>
    /// <param name="builder">The .NET application host builder.</param>
    /// <returns>The same builder so registrations can be chained.</returns>
    public static IHostApplicationBuilder AddIndexingServices(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<AzureServicesOptions>()
            .BindConfiguration(AzureServicesOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                options => options.ChunkOverlapCharacters < options.MaxChunkCharacters,
                "Azure:ChunkOverlapCharacters must be smaller than Azure:MaxChunkCharacters.")
            .ValidateOnStart();

        // DefaultAzureCredential resolves to the Function App managed identity after deployment.
        builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
        builder.Services.AddSingleton<AzureBlobService>();
        builder.Services.AddSingleton<DocumentIntelligenceService>();
        builder.Services.AddSingleton<AzureSearchService>();
        builder.Services.AddSingleton<ProcessedEventMemory>();

        return builder;
    }
}
