using System.Text.Json.Serialization;

namespace AzureAISearchIndexer.Models;

/// <summary>
/// Represents one page chunk stored in Azure AI Search.
/// </summary>
public sealed class SearchDocumentChunk
{
    /// <summary>
    /// Creates one document sent to Azure AI Search.
    /// </summary>
    /// <param name="id">The unique Azure AI Search document key.</param>
    /// <param name="documentId">The identifier shared by all chunks from one PDF.</param>
    /// <param name="fileName">The PDF file name.</param>
    /// <param name="pageNumber">The original PDF page number.</param>
    /// <param name="content">The searchable text.</param>
    /// <param name="blobName">The private blob identifier used by the API.</param>
    [JsonConstructor]
    public SearchDocumentChunk(
        string id,
        string documentId,
        string fileName,
        int pageNumber,
        string content,
        string blobName)
    {
        Id = id;
        DocumentId = documentId;
        FileName = fileName;
        PageNumber = pageNumber;
        Content = content;
        BlobName = blobName;
    }

    [JsonPropertyName("id")]
    public string Id { get; }

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
