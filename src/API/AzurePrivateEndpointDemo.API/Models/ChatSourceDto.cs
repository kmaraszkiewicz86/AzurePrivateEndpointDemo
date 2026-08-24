namespace AzurePrivateEndpointDemo.API.Models;

/// <summary>
/// Identifies a PDF page used as a chatbot source.
/// </summary>
public sealed class ChatSourceDto
{
    /// <summary>
    /// Creates a source reference for one indexed PDF page.
    /// </summary>
    /// <param name="fileName">The PDF file name.</param>
    /// <param name="pageNumber">The one-based PDF page number.</param>
    /// <param name="documentId">The indexed document identifier.</param>
    /// <param name="downloadUrl">The API URL used to download the private PDF.</param>
    public ChatSourceDto(string fileName, int pageNumber, string documentId, string downloadUrl)
    {
        FileName = fileName;
        PageNumber = pageNumber;
        DocumentId = documentId;
        DownloadUrl = downloadUrl;
    }

    public string FileName { get; }

    public int PageNumber { get; }

    public string DocumentId { get; }

    public string DownloadUrl { get; }
}
