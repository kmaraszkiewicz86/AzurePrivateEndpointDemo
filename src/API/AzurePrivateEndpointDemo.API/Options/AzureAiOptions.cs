using System.ComponentModel.DataAnnotations;

namespace AzurePrivateEndpointDemo.API.Options;

/// <summary>
/// Configures Azure AI Search and Azure OpenAI access for chatbot answers.
/// </summary>
public sealed class AzureAiOptions
{
    public const string SectionName = "AzureAI";

    [Required, Url]
    public string SearchEndpoint { get; init; } = string.Empty;

    [Required]
    public string SearchIndexName { get; init; } = "documents";

    [Required, Url]
    public string OpenAIEndpoint { get; init; } = string.Empty;

    [Required]
    public string OpenAIChatDeployment { get; init; } = string.Empty;

    [Range(1, 20)]
    public int SearchResultCount { get; init; } = 5;
}
