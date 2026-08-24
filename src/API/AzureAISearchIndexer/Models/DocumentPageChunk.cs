namespace AzureAISearchIndexer.Models;

/// <summary>
/// Contains text extracted from one PDF page or part of a page.
/// </summary>
public sealed class DocumentPageChunk
{
    /// <summary>
    /// Creates one page-aware text chunk.
    /// </summary>
    /// <param name="pageNumber">The original one-based PDF page number.</param>
    /// <param name="chunkNumber">The chunk position within the page.</param>
    /// <param name="content">The extracted text.</param>
    public DocumentPageChunk(int pageNumber, int chunkNumber, string content)
    {
        PageNumber = pageNumber;
        ChunkNumber = chunkNumber;
        Content = content;
    }

    public int PageNumber { get; }

    public int ChunkNumber { get; }

    public string Content { get; }
}
