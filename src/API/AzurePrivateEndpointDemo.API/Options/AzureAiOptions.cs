using System.ComponentModel.DataAnnotations;

namespace AzurePrivateEndpointDemo.API.Options;

/// <summary>
/// Configures Azure AI Search access for browsing indexed files and their content.
/// </summary>
public sealed class AzureAiOptions
{
    public const string SectionName = "AzureAI";

    [Required, Url]
    public string SearchEndpoint { get; init; } = string.Empty;

    [Required]
    public string SearchIndexName { get; init; } = "documents";

}
