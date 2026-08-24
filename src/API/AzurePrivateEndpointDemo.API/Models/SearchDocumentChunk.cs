using System.Text.Json.Serialization;

namespace AzurePrivateEndpointDemo.API.Models;

/// <summary>
/// Represents an indexed PDF chunk retrieved from Azure AI Search.
/// </summary>
internal sealed class SearchDocumentChunk
{
    [JsonConstructor]
    public SearchDocumentChunk(
        string documentId,
        string fileName,
        int pageNumber,
        string content,
        string blobName)
    {
        DocumentId = documentId;
        FileName = fileName;
        PageNumber = pageNumber;
        Content = content;
        BlobName = blobName;
    }

    [JsonPropertyName("documentId")]
    public string DocumentId { get; }

    [JsonPropertyName("fileName")]
    public string FileName { get; }

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; }

    [JsonPropertyName("content")]
    public string Content { get; }

    [JsonPropertyName("blobName")]
    public string BlobName { get; }
}
