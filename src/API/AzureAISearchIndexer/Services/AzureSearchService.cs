using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using AzureAISearchIndexer.Models;
using AzureAISearchIndexer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzureAISearchIndexer.Services;

/// <summary>
/// Creates the required Azure AI Search index and manages indexed PDF chunks.
/// </summary>
public sealed class AzureSearchService
{
    private static readonly string[] RequiredFieldNames =
        ["id", "documentId", "fileName", "pageNumber", "content", "blobName"];

    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly string _indexName;
    private readonly ILogger<AzureSearchService> _logger;

    /// <summary>
    /// Creates Azure AI Search management and document clients for the configured index.
    /// </summary>
    /// <param name="credential">The Managed Identity compatible Azure credential.</param>
    /// <param name="options">The configured Azure AI Search endpoint and index name.</param>
    /// <param name="logger">The service logger.</param>
    public AzureSearchService(
        TokenCredential credential,
        IOptions<AzureServicesOptions> options,
        ILogger<AzureSearchService> logger)
    {
        AzureServicesOptions configuration = options.Value;
        var endpoint = new Uri(configuration.SearchEndpoint);
        _indexName = configuration.SearchIndexName;
        _indexClient = new SearchIndexClient(endpoint, credential);
        _searchClient = _indexClient.GetSearchClient(_indexName);
        _logger = logger;
    }

    /// <summary>
    /// Creates the search index when missing and updates it when required fields are missing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the Azure AI Search operation.</param>
    public async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            SearchIndex existingIndex = (await _indexClient.GetIndexAsync(_indexName, cancellationToken)).Value;
            HashSet<string> existingFields = existingIndex.Fields
                .Select(field => field.Name)
                .ToHashSet(StringComparer.Ordinal);

            if (RequiredFieldNames.All(existingFields.Contains))
            {
                return;
            }
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            _logger.LogInformation("Azure AI Search index {IndexName} does not exist and will be created.", _indexName);
        }

        await _indexClient.CreateOrUpdateIndexAsync(CreateIndexDefinition(), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Replaces all chunks associated with a blob and uploads the current page chunks.
    /// </summary>
    /// <param name="blobName">The unique Blob Storage name used for API downloads.</param>
    /// <param name="fileName">The unique PDF file name used to find its search chunks.</param>
    /// <param name="pageChunks">The page-aware text extracted by Document Intelligence.</param>
    /// <param name="cancellationToken">Cancels the Azure AI Search operation.</param>
    public async Task IndexDocumentAsync(
        string blobName,
        string fileName,
        IReadOnlyList<DocumentPageChunk> pageChunks,
        CancellationToken cancellationToken)
    {
        // A file name is unique in the configured container, so it identifies all chunks to replace.
        await DeleteByFileNameAsync(fileName, cancellationToken);

        SearchDocumentChunk[] searchDocuments = pageChunks
            .Select(chunk => new SearchDocumentChunk(
                Guid.NewGuid().ToString("N"),
                fileName,
                fileName,
                chunk.PageNumber,
                chunk.Content,
                blobName))
            .ToArray();

        foreach (SearchDocumentChunk[] batch in searchDocuments.Chunk(500))
        {
            await _searchClient.UploadDocumentsAsync(
                batch,
                new IndexDocumentsOptions { ThrowOnAnyError = true },
                cancellationToken);
        }
    }

    /// <summary>
    /// Deletes every indexed chunk associated with the specified blob.
    /// </summary>
    /// <param name="blobName">The deleted blob name, which is also the indexed file name.</param>
    /// <param name="cancellationToken">Cancels the Azure AI Search operation.</param>
    /// <returns><see langword="true"/> when matching chunks were found and deleted.</returns>
    public Task<bool> DeleteDocumentAsync(string blobName, CancellationToken cancellationToken) =>
        DeleteByFileNameAsync(blobName, cancellationToken);

    private async Task<bool> DeleteByFileNameAsync(string fileName, CancellationToken cancellationToken)
    {
        string escapedFileName = fileName.Replace("'", "''", StringComparison.Ordinal);
        var documentKeys = new List<string>();
        var options = new SearchOptions
        {
            Filter = $"fileName eq '{escapedFileName}'",
            Size = 1000
        };
        options.Select.Add("id");

        SearchResults<SearchDocument> results = await _searchClient.SearchAsync<SearchDocument>(
            "*",
            options,
            cancellationToken);

        await foreach (SearchResult<SearchDocument> result in results.GetResultsAsync())
        {
            if (result.Document.TryGetValue("id", out object? value) && value is string key)
            {
                documentKeys.Add(key);
            }
        }

        foreach (string[] batch in documentKeys.Chunk(500))
        {
            await _searchClient.DeleteDocumentsAsync(
                "id",
                batch,
                new IndexDocumentsOptions { ThrowOnAnyError = true },
                cancellationToken);
        }

        return documentKeys.Count > 0;
    }

    private SearchIndex CreateIndexDefinition()
    {
        SearchField[] fields =
        [
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
            new SimpleField("documentId", SearchFieldDataType.String) { IsFilterable = true },
            new SearchableField("fileName") { IsFilterable = true },
            new SimpleField("pageNumber", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
            new SearchableField("content"),
            new SimpleField("blobName", SearchFieldDataType.String) { IsFilterable = true }
        ];

        return new SearchIndex(_indexName, fields);
    }
}
