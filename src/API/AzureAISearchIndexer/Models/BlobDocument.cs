namespace AzureAISearchIndexer.Models;

/// <summary>
/// Contains a downloaded PDF and its original file name.
/// </summary>
public sealed class BlobDocument
{
    /// <summary>
    /// Creates a downloaded blob document.
    /// </summary>
    /// <param name="content">The downloaded PDF bytes.</param>
    /// <param name="fileName">The original PDF file name.</param>
    public BlobDocument(BinaryData content, string fileName)
    {
        Content = content;
        FileName = fileName;
    }

    public BinaryData Content { get; }

    public string FileName { get; }
}
