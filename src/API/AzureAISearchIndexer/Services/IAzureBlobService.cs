using AzureAISearchIndexer.Models;

namespace AzureAISearchIndexer.Services;

/// <summary>
/// Validates blob event paths and reads PDFs from the configured container.
/// </summary>
public interface IAzureBlobService
{
    /// <summary>
    /// Checks the event URL's container and extracts its decoded blob name.
    /// </summary>
    bool TryGetBlobName(string blobUrl, out string blobName);

    /// <summary>
    /// Downloads PDF bytes and the original file name.
    /// </summary>
    Task<BlobDocument> DownloadPdfAsync(string blobName, CancellationToken cancellationToken);

    /// <summary>
    /// Checks access to the configured Blob Storage container.
    /// </summary>
    Task CheckConfigurationAsync(CancellationToken cancellationToken);
}
