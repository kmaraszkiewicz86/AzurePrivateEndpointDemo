namespace AzurePrivateEndpointDemo.API.Models;

/// <summary>
/// Represents one indexed PDF file and its extracted page content.
/// </summary>
public sealed class IndexedDocumentDto(
    string documentId,
    string fileName,
    string downloadUrl,
    IReadOnlyList<IndexedDocumentPageDto> pages)
{
    public string DocumentId { get; } = documentId;
    public string FileName { get; } = fileName;
    public string DownloadUrl { get; } = downloadUrl;
    public IReadOnlyList<IndexedDocumentPageDto> Pages { get; } = pages;
}
