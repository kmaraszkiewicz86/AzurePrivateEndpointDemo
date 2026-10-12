using Azure;
using Azure.AI.DocumentIntelligence;
using AzureAISearchIndexer.Models;
using AzureAISearchIndexer.Options;
using Microsoft.Extensions.Options;

namespace AzureAISearchIndexer.Services;

/// <summary>
/// Extracts page-aware text from PDF content with Azure AI Document Intelligence.
/// </summary>
public sealed class DocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly DocumentIntelligenceClient _client;
    private readonly DocumentIntelligenceAdministrationClient _administrationClient;
    private readonly AzureServicesOptions _options;

    /// <summary>
    /// Uses the Document Intelligence clients created by dependency registration.
    /// </summary>
    /// <param name="client">The client used to analyze PDFs.</param>
    /// <param name="administrationClient">The client used to check resource access.</param>
    /// <param name="options">The configured analysis model and chunk settings.</param>
    public DocumentIntelligenceService(
        DocumentIntelligenceClient client,
        DocumentIntelligenceAdministrationClient administrationClient,
        IOptions<AzureServicesOptions> options)
    {
        _options = options.Value;
        _client = client;
        _administrationClient = administrationClient;
    }

    /// <inheritdoc />
    public async Task CheckConfigurationAsync(CancellationToken cancellationToken) =>
        await _administrationClient.GetResourceDetailsAsync(cancellationToken);

    /// <summary>
    /// Analyzes a PDF and splits extracted page text into configured chunk sizes.
    /// </summary>
    /// <param name="pdfContent">The PDF bytes downloaded from Blob Storage.</param>
    /// <param name="cancellationToken">Cancels the Document Intelligence operation.</param>
    /// <returns>Page-aware text chunks ready for Azure AI Search.</returns>
    public async Task<IReadOnlyList<DocumentPageChunk>> ExtractPageChunksAsync(
        BinaryData pdfContent,
        CancellationToken cancellationToken)
    {
        Operation<AnalyzeResult> operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            _options.DocumentIntelligenceModelId,
            pdfContent,
            cancellationToken);

        var chunks = new List<DocumentPageChunk>();
        // Chunk each page separately so every search result keeps its original PDF page number.
        foreach (DocumentPage page in operation.Value.Pages)
        {
            string pageContent = string.Join(Environment.NewLine, page.Lines.Select(line => line.Content)).Trim();
            int chunkNumber = 0;

            foreach (string content in SplitText(pageContent))
            {
                chunkNumber++;
                chunks.Add(new DocumentPageChunk(page.PageNumber, chunkNumber, content));
            }
        }

        return chunks;
    }

    private IEnumerable<string> SplitText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        int start = 0;
        while (start < text.Length)
        {
            int length = Math.Min(_options.MaxChunkCharacters, text.Length - start);
            int end = start + length;

            if (end < text.Length)
            {
                int lineBreak = text.LastIndexOf('\n', end - 1, length);
                if (lineBreak > start + (_options.MaxChunkCharacters / 2))
                {
                    end = lineBreak;
                }
            }

            string chunk = text[start..end].Trim();
            if (chunk.Length > 0)
            {
                yield return chunk;
            }

            if (end >= text.Length)
            {
                yield break;
            }

            start = Math.Max(end - _options.ChunkOverlapCharacters, start + 1);
        }
    }
}
