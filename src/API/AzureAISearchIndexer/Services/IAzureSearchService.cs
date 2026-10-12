using AzureAISearchIndexer.Models;

namespace AzureAISearchIndexer.Services;

/// <summary>
/// Manages the Azure AI Search index and indexed PDF chunks.
/// </summary>
public interface IAzureSearchService
{
    /// <summary>
    /// Creates the search index or updates it when required fields are missing.
    /// </summary>
    Task EnsureIndexExistsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks access to the configured Azure AI Search service.
    /// </summary>
    Task CheckConfigurationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a blob's indexed chunks with its current extracted content.
    /// </summary>
    Task IndexDocumentAsync(
        string blobName,
        string fileName,
        IReadOnlyList<DocumentPageChunk> pageChunks,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the blob's indexed chunks and reports whether any were found.
    /// </summary>
    Task<bool> DeleteDocumentAsync(string blobName, CancellationToken cancellationToken);
}
