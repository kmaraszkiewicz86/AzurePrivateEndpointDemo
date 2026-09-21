using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using AzurePrivateEndpointDemo.API.Models;

namespace AzurePrivateEndpointDemo.API.Services;

/// <summary>
/// Lists indexed PDF files and their extracted text directly from Azure AI Search.
/// </summary>
public sealed class IndexedDocumentService(SearchClient searchClient)
{
    /// <summary>
    /// Retrieves indexed chunks and groups them by file and page without generating AI answers.
    /// </summary>
    /// <param name="cancellationToken">Cancels search requests, including subsequent result pages.</param>
    /// <returns>Indexed files with their page content and API download URLs.</returns>
    public async Task<IReadOnlyList<IndexedDocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken)
    {
        const int batchSize = 1000;
        var options = new SearchOptions { Size = batchSize, Skip = 0 };
        options.Select.Add("documentId");
        options.Select.Add("fileName");
        options.Select.Add("pageNumber");
        options.Select.Add("content");
        options.Select.Add("blobName");

        var chunks = new List<SearchDocumentChunk>();
        // Read every batch, not only the first page or the top matches used by the former chatbot.
        while (true)
        {
            SearchResults<SearchDocumentChunk> results = await searchClient.SearchAsync<SearchDocumentChunk>(
                "*", options, cancellationToken);

            int count = 0;
            await foreach (SearchResult<SearchDocumentChunk> result in results.GetResultsAsync().WithCancellation(cancellationToken))
            {
                chunks.Add(result.Document);
                count++;
            }

            if (count < batchSize) break;
            options.Skip += count;
        }

        return chunks
            .GroupBy(chunk => chunk.BlobName, StringComparer.Ordinal)
            .Select(file => new IndexedDocumentDto(
                file.First().DocumentId,
                file.First().FileName,
                $"/api/Documents/{Uri.EscapeDataString(file.Key)}",
                file.GroupBy(chunk => chunk.PageNumber)
                    .OrderBy(page => page.Key)
                    .Select(page => new IndexedDocumentPageDto(
                        page.Key, page.Select(chunk => chunk.Content).ToArray()))
                    .ToArray()))
            .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
