using System.ComponentModel.DataAnnotations;

namespace AzurePrivateEndpointDemo.API.Options;

/// <summary>
/// Configures document access through private Azure Blob Storage.
/// </summary>
public sealed class AzureStorageOptions
{
    public const string SectionName = "AzureStorage";

    [Required, Url]
    public string BlobServiceUri { get; init; } = string.Empty;

    [Required]
    public string DocumentContainerName { get; init; } = "chatbot-documents";

    [Range(1, 100 * 1024 * 1024)]
    public long MaxUploadBytes { get; init; } = 20 * 1024 * 1024;
}
