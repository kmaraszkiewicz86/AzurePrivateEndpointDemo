namespace AzurePrivateEndpointDemo.API.Models;

/// <summary>
/// Contains the stream and response metadata for a downloaded PDF.
/// </summary>
public sealed class DownloadedDocument
{
    /// <summary>
    /// Creates a downloadable PDF result.
    /// </summary>
    /// <param name="content">The private blob content stream.</param>
    /// <param name="contentType">The HTTP content type.</param>
    /// <param name="fileName">The file name sent in the HTTP response.</param>
    public DownloadedDocument(Stream content, string contentType, string fileName)
    {
        Content = content;
        ContentType = contentType;
        FileName = fileName;
    }

    public Stream Content { get; }

    public string ContentType { get; }

    public string FileName { get; }
}
