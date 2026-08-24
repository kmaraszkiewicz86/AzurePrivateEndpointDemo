using System.Text.Json.Serialization;
using Azure.Messaging;
using AzureAISearchIndexer.Models;
using AzureAISearchIndexer.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureAISearchIndexer;

/// <summary>
/// Handles Blob Storage create and delete events and synchronizes PDF content with Azure AI Search.
/// </summary>
public sealed class AzureAISearchIndexerFunction(
    AzureBlobService blobService,
    DocumentIntelligenceService documentIntelligenceService,
    AzureSearchService searchService,
    ProcessedEventMemory processedEventMemory,
    ILogger<AzureAISearchIndexerFunction> logger)
{
    private const string BlobCreatedEventType = "Microsoft.Storage.BlobCreated";
    private const string BlobDeletedEventType = "Microsoft.Storage.BlobDeleted";

    /// <summary>
    /// Receives one Event Grid event and delegates it to the create or delete method.
    /// </summary>
    /// <param name="cloudEvent">The Blob Storage event delivered by Event Grid.</param>
    /// <param name="cancellationToken">Cancels the indexing operation.</param>
    [Function(nameof(AzureAISearchIndexerFunction))]
    public async Task RunAsync(
        [EventGridTrigger] CloudEvent cloudEvent,
        CancellationToken cancellationToken)
    {
        await searchService.EnsureIndexExistsAsync(cancellationToken);

        if (cloudEvent.Type is not BlobCreatedEventType and not BlobDeletedEventType)
        {
            logger.LogInformation("Ignoring unsupported Event Grid event type {EventType}.", cloudEvent.Type);
            return;
        }

        StorageBlobEventData? eventData = cloudEvent.Data?.ToObjectFromJson<StorageBlobEventData>();
        if (eventData is null || !blobService.TryGetBlobName(eventData.Url, out string blobName))
        {
            logger.LogWarning("Ignoring Event Grid event {EventId} because its blob URL is invalid or belongs to another container.", cloudEvent.Id);
            return;
        }

        if (cloudEvent.Type == BlobCreatedEventType)
        {
            await HandleBlobCreatedAsync(cloudEvent.Id, blobName, cancellationToken);
            return;
        }

        await HandleBlobDeletedAsync(cloudEvent.Id, blobName, cancellationToken);
    }

    private async Task HandleBlobCreatedAsync(
        string eventId,
        string blobName,
        CancellationToken cancellationToken)
    {
        if (processedEventMemory.WasProcessed(eventId))
        {
            logger.LogInformation("BlobCreated event {EventId} was already processed.", eventId);
            return;
        }

        BlobDocument blobDocument = await blobService.DownloadPdfAsync(blobName, cancellationToken);
        IReadOnlyList<DocumentPageChunk> pageChunks = await documentIntelligenceService.ExtractPageChunksAsync(
            blobDocument.Content,
            cancellationToken);

        await searchService.IndexDocumentAsync(
            blobName,
            blobDocument.FileName,
            pageChunks,
            cancellationToken);

        // Mark the event only after every Azure operation succeeds so Event Grid can retry failures.
        processedEventMemory.MarkAsProcessed(eventId);
        logger.LogInformation("BlobCreated event {EventId} was processed successfully.", eventId);
    }

    private async Task HandleBlobDeletedAsync(
        string eventId,
        string blobName,
        CancellationToken cancellationToken)
    {
        if (processedEventMemory.WasProcessed(eventId))
        {
            logger.LogInformation("BlobDeleted event {EventId} was already processed.", eventId);
            return;
        }

        bool documentExisted = await searchService.DeleteDocumentAsync(blobName, cancellationToken);
        // Mark the event only after the search delete has completed successfully.
        processedEventMemory.MarkAsProcessed(eventId);

        logger.LogInformation(
            documentExisted
                ? "BlobDeleted event {EventId} removed the document from Azure AI Search."
                : "BlobDeleted event {EventId} found no matching document in Azure AI Search.",
            eventId);
    }

    private sealed class StorageBlobEventData
    {
        /// <summary>
        /// Creates the minimal Blob Storage event payload required by this Function.
        /// </summary>
        /// <param name="url">The absolute URL of the created or deleted blob.</param>
        [JsonConstructor]
        public StorageBlobEventData(string url)
        {
            Url = url;
        }

        [JsonPropertyName("url")]
        public string Url { get; }
    }
}
