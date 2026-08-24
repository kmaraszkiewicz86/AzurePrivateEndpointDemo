namespace AzurePrivateEndpointDemo.API.Models;

/// <summary>
/// Describes a PDF uploaded to private Blob Storage.
/// </summary>
public sealed class DocumentUploadResponse
{
    /// <summary>
    /// Creates the response returned after a successful PDF upload.
    /// </summary>
    /// <param name="blobName">The unique Blob Storage name.</param>
    /// <param name="fileName">The original PDF file name.</param>
    /// <param name="downloadUrl">The API URL used to download the PDF.</param>
    public DocumentUploadResponse(string blobName, string fileName, string downloadUrl)
    {
        BlobName = blobName;
        FileName = fileName;
        DownloadUrl = downloadUrl;
    }

    public string BlobName { get; }

    public string FileName { get; }

    public string DownloadUrl { get; }
}
