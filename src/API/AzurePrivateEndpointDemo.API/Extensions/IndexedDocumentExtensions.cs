using Azure.Core;
using Azure.Search.Documents;
using AzurePrivateEndpointDemo.API.Options;
using AzurePrivateEndpointDemo.API.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AzurePrivateEndpointDemo.API.Extensions;

/// <summary>
/// Registers the Managed Identity client used to browse indexed documents.
/// </summary>
public static class IndexedDocumentExtensions
{
    /// <summary>
    /// Adds Azure AI Search and the indexed document service.
    /// </summary>
    /// <param name="builder">The .NET application host builder.</param>
    /// <returns>The same builder so registrations can be chained.</returns>
    public static IHostApplicationBuilder AddIndexedDocumentServices(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<AzureAiOptions>()
            .BindConfiguration(AzureAiOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Search reuses the TokenCredential registered for the API managed identity.
        builder.Services.AddSingleton(serviceProvider =>
        {
            AzureAiOptions configuration = serviceProvider
                .GetRequiredService<IOptions<AzureAiOptions>>()
                .Value;
            TokenCredential credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new SearchClient(
                new Uri(configuration.SearchEndpoint),
                configuration.SearchIndexName,
                credential);
        });
        builder.Services.AddSingleton<IndexedDocumentService>();

        return builder;
    }
}
