using System.ComponentModel.DataAnnotations;

namespace AzureAISearchIndexer.Options;

/// <summary>
/// Configures the private Azure services used by the Function App.
/// </summary>
public sealed class AzureServicesOptions
{
    public const string SectionName = "Azure";

    [Required, Url]
    public string BlobServiceUri { get; init; } = string.Empty;

    [Required]
    public string DocumentContainerName { get; init; } = "chatbot-documents";

    [Required, Url]
    public string SearchEndpoint { get; init; } = string.Empty;

    [Required]
    public string SearchIndexName { get; init; } = "documents";

    [Required, Url]
    public string DocumentIntelligenceEndpoint { get; init; } = string.Empty;

    [Required]
    public string DocumentIntelligenceModelId { get; init; } = "prebuilt-layout";

    [Range(500, 16000)]
    public int MaxChunkCharacters { get; init; } = 4000;

    [Range(0, 2000)]
    public int ChunkOverlapCharacters { get; init; } = 300;
}
