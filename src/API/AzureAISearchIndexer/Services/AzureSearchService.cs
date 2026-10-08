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
    private volatile bool _indexVerified;

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
        // The index schema does not change at runtime, so one successful check per instance is enough.
        if (_indexVerified)
        {
            return;
        }

        try
        {
            SearchIndex existingIndex = (await _indexClient.GetIndexAsync(_indexName, cancellationToken)).Value;
            HashSet<string> existingFields = existingIndex.Fields
                .Select(field => field.Name)
                .ToHashSet(StringComparer.Ordinal);

            if (RequiredFieldNames.All(existingFields.Contains))
            {
                _indexVerified = true;
                return;
            }
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            _logger.LogInformation("Azure AI Search index {IndexName} does not exist and will be created.", _indexName);
        }

        await _indexClient.CreateOrUpdateIndexAsync(CreateIndexDefinition(), cancellationToken: cancellationToken);
        _indexVerified = true;
    }

    public async Task CheckConfigurationAsync(CancellationToken cancellationToken) =>
        await _indexClient.GetServiceStatisticsAsync(cancellationToken);

    /// <summary>
    /// Replaces all chunks associated with a blob and uploads the current page chunks.
    /// </summary>
    /// <param name="blobName">The unique Blob Storage name used for API downloads.</param>
    /// <param name="fileName">The original PDF file name shown to users.</param>
    /// <param name="pageChunks">The page-aware text extracted by Document Intelligence.</param>
    /// <param name="cancellationToken">Cancels the Azure AI Search operation.</param>
    public async Task IndexDocumentAsync(
        string blobName,
        string fileName,
        IReadOnlyList<DocumentPageChunk> pageChunks,
        CancellationToken cancellationToken)
    {
        // A blob name is unique in the container, while original file names can repeat across uploads.
        await DeleteByBlobNameAsync(blobName, cancellationToken);

        SearchDocumentChunk[] searchDocuments = pageChunks
            .Select(chunk => new SearchDocumentChunk(
                Guid.NewGuid().ToString("N"),
                blobName,
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
    /// <param name="blobName">The deleted blob name stored in the <c>blobName</c> field of every chunk.</param>
    /// <param name="cancellationToken">Cancels the Azure AI Search operation.</param>
    /// <returns><see langword="true"/> when matching chunks were found and deleted.</returns>
    public Task<bool> DeleteDocumentAsync(string blobName, CancellationToken cancellationToken) =>
        DeleteByBlobNameAsync(blobName, cancellationToken);

    private async Task<bool> DeleteByBlobNameAsync(string blobName, CancellationToken cancellationToken)
    {
        const int PageSize = 1000;
        string escapedBlobName = blobName.Replace("'", "''", StringComparison.Ordinal);
        var documentKeys = new List<string>();
        int skip = 0;
        int pageCount;

        // Collect every key before deleting so removed documents cannot shift the Skip offsets.
        // OrderBy is not used because the id field is not sortable in the existing index.
        do
        {
            var options = new SearchOptions
            {
                Filter = $"blobName eq '{escapedBlobName}'",
                Size = PageSize,
                Skip = skip
            };
            options.Select.Add("id");

            SearchResults<SearchDocument> results = await _searchClient.SearchAsync<SearchDocument>(
                "*",
                options,
                cancellationToken);

            pageCount = 0;
            await foreach (SearchResult<SearchDocument> result in results.GetResultsAsync())
            {
                pageCount++;
                if (result.Document.TryGetValue("id", out object? value) && value is string key)
                {
                    documentKeys.Add(key);
                }
            }

            skip += pageCount;
        }
        while (pageCount == PageSize);

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
