using System.Text;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AzureAISearchIndexer.Models;
using AzureAISearchIndexer.Options;
using Microsoft.Extensions.Options;

namespace AzureAISearchIndexer.Services;

/// <summary>
/// Reads PDF files from the configured private Blob Storage container.
/// </summary>
public sealed class AzureBlobService
{
    private const string OriginalFileNameMetadataKey = "originalfilename";
    private readonly BlobContainerClient _containerClient;
    private readonly string _containerName;

    /// <summary>
    /// Creates Blob Storage clients for the container selected in configuration.
    /// </summary>
    /// <param name="credential">The Managed Identity compatible Azure credential.</param>
    /// <param name="options">The configured Azure endpoints and container name.</param>
    public AzureBlobService(
        TokenCredential credential,
        IOptions<AzureServicesOptions> options)
    {
        AzureServicesOptions configuration = options.Value;
        _containerName = configuration.DocumentContainerName;
        _containerClient = new BlobServiceClient(new Uri(configuration.BlobServiceUri), credential)
            .GetBlobContainerClient(_containerName);
    }

    /// <summary>
    /// Validates the event URL against the configured container and extracts the blob name.
    /// </summary>
    /// <param name="blobUrl">The absolute blob URL received from Event Grid.</param>
    /// <param name="blobName">The decoded blob name when validation succeeds.</param>
    /// <returns><see langword="true"/> when the URL belongs to the configured container.</returns>
    public bool TryGetBlobName(string blobUrl, out string blobName)
    {
        blobName = string.Empty;
        if (!Uri.TryCreate(blobUrl, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        string path = uri.AbsolutePath.TrimStart('/');
        int separatorIndex = path.IndexOf('/');
        if (separatorIndex <= 0)
        {
            return false;
        }

        string containerName = Uri.UnescapeDataString(path[..separatorIndex]);
        if (!string.Equals(containerName, _containerName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        blobName = Uri.UnescapeDataString(path[(separatorIndex + 1)..]);
        return !string.IsNullOrWhiteSpace(blobName);
    }

    /// <summary>
    /// Downloads a PDF with Managed Identity and returns its content and original file name.
    /// </summary>
    /// <param name="blobName">The unique name of the PDF blob.</param>
    /// <param name="cancellationToken">Cancels the Blob Storage operation.</param>
    /// <returns>The downloaded PDF content and its original file name.</returns>
    public async Task<BlobDocument> DownloadPdfAsync(string blobName, CancellationToken cancellationToken)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(blobName);
        Azure.Response<BlobDownloadResult> response = await blobClient.DownloadContentAsync(cancellationToken);
        string fileName = ReadOriginalFileName(response.Value.Details.Metadata, blobName);

        return new BlobDocument(response.Value.Content, fileName);
    }

    private static string ReadOriginalFileName(IDictionary<string, string> metadata, string blobName)
    {
        if (!metadata.TryGetValue(OriginalFileNameMetadataKey, out string? encodedFileName))
        {
            return Path.GetFileName(blobName);
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encodedFileName));
        }
        catch (FormatException)
        {
            return Path.GetFileName(blobName);
        }
    }
}
