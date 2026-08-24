using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AzurePrivateEndpointDemo.API.Models;
using AzurePrivateEndpointDemo.API.Options;
using Microsoft.Extensions.Options;

namespace AzurePrivateEndpointDemo.API.Services;

/// <summary>
/// Handles PDF validation and private Blob Storage upload, download, and deletion.
/// </summary>
public sealed class DocumentService(
    BlobContainerClient containerClient,
    IOptions<AzureStorageOptions> options)
{
    private const string OriginalFileNameMetadataKey = "originalfilename";
    private readonly AzureStorageOptions _options = options.Value;

    /// <summary>
    /// Validates and uploads one PDF to the configured private container.
    /// </summary>
    /// <param name="file">The uploaded multipart PDF.</param>
    /// <param name="cancellationToken">Cancels the Blob Storage operation.</param>
    /// <returns>The blob name, original file name, and API download URL.</returns>
    public async Task<DocumentUploadResponse> UploadAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        ValidateFile(file);

        string originalFileName = Path.GetFileName(file.FileName);
        await using Stream input = file.OpenReadStream();
        await ValidatePdfSignatureAsync(input, cancellationToken);

        input.Position = 0;
        // The file name is also the blob name, so it uniquely identifies the PDF in this container.
        string blobName = originalFileName;
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(
            input,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" },
                Metadata = new Dictionary<string, string>
                {
                    [OriginalFileNameMetadataKey] = Convert.ToBase64String(Encoding.UTF8.GetBytes(originalFileName))
                }
            },
            cancellationToken);

        string downloadUrl = CreateDownloadUrl(blobName);
        return new DocumentUploadResponse(blobName, originalFileName, downloadUrl);
    }

    /// <summary>
    /// Downloads one private PDF or returns <see langword="null"/> when it does not exist.
    /// </summary>
    /// <param name="blobName">The unique blob name.</param>
    /// <param name="cancellationToken">Cancels the Blob Storage operation.</param>
    /// <returns>The downloadable document, or <see langword="null"/> when it is missing.</returns>
    public async Task<DownloadedDocument?> DownloadAsync(
        string blobName,
        CancellationToken cancellationToken)
    {
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        try
        {
            Response<BlobProperties> properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            Response<BlobDownloadStreamingResult> download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);

            return new DownloadedDocument(
                download.Value.Content,
                properties.Value.ContentType ?? "application/pdf",
                ReadOriginalFileName(properties.Value.Metadata, blobName));
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes one private PDF and reports whether it existed.
    /// </summary>
    /// <param name="blobName">The unique blob name.</param>
    /// <param name="cancellationToken">Cancels the Blob Storage operation.</param>
    /// <returns><see langword="true"/> when the blob was deleted; otherwise <see langword="false"/>.</returns>
    public async Task<bool> DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        BlobClient blobClient = containerClient.GetBlobClient(blobName);
        Response<bool> response = await blobClient.DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots,
            cancellationToken: cancellationToken);

        return response.Value;
    }

    private void ValidateFile(IFormFile file)
    {
        if (file.Length == 0)
        {
            throw new InvalidDataException("Choose a non-empty PDF file.");
        }

        if (file.Length > _options.MaxUploadBytes)
        {
            throw new InvalidDataException($"The PDF cannot exceed {_options.MaxUploadBytes / (1024 * 1024)} MB.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Only PDF files are supported.");
        }
    }

    private static async Task ValidatePdfSignatureAsync(Stream input, CancellationToken cancellationToken)
    {
        var signature = new byte[5];
        int bytesRead = await input.ReadAsync(signature, cancellationToken);

        if (bytesRead != signature.Length || Encoding.ASCII.GetString(signature) != "%PDF-")
        {
            throw new InvalidDataException("The uploaded file does not have a valid PDF signature.");
        }
    }

    private static string ReadOriginalFileName(IDictionary<string, string> metadata, string blobName)
    {
        if (!metadata.TryGetValue(OriginalFileNameMetadataKey, out string? encodedFileName))
        {
            return blobName;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encodedFileName));
        }
        catch (FormatException)
        {
            return blobName;
        }
    }

    private static string CreateDownloadUrl(string blobName) =>
        $"/api/documents/{Uri.EscapeDataString(blobName)}";
}
