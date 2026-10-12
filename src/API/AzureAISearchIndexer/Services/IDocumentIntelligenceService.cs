using AzureAISearchIndexer.Models;

namespace AzureAISearchIndexer.Services;

/// <summary>
/// Extracts page-aware text chunks from PDF content.
/// </summary>
public interface IDocumentIntelligenceService
{
    /// <summary>
    /// Analyzes a PDF and splits extracted page text into chunks.
    /// </summary>
    Task<IReadOnlyList<DocumentPageChunk>> ExtractPageChunksAsync(
        BinaryData pdfContent,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks access to the configured Document Intelligence resource.
    /// </summary>
    Task CheckConfigurationAsync(CancellationToken cancellationToken);
}
