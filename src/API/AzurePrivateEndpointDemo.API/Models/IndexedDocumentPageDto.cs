namespace AzurePrivateEndpointDemo.API.Models;

/// <summary>
/// Contains the indexed text chunks for one PDF page.
/// </summary>
public sealed class IndexedDocumentPageDto(int pageNumber, IReadOnlyList<string> chunks)
{
    public int PageNumber { get; } = pageNumber;

    // The current index does not store chunk positions. Keep chunks separate instead of
    // presenting their search order as a reconstructed page or removing overlapping text.
    public IReadOnlyList<string> Chunks { get; } = chunks;
}
