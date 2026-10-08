using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging;
using Azure;
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
    private readonly SemaphoreSlim availabilityCheckLock = new(1, 1);
    private bool servicesAvailable;

    /// <summary>
    /// Receives one Blob Storage event from Azure Queue Storage and delegates it to the create or delete method.
    /// </summary>
    /// <param name="message">The CloudEvents JSON written to the queue by the Event Grid subscription.</param>
    /// <param name="cancellationToken">Cancels the indexing operation.</param>
    [Function(nameof(AzureAISearchIndexerFunction))]
    public async Task RunAsync(
        [QueueTrigger("%DocumentEventsQueueName%", Connection = "DocumentEventsQueue")] string message,
        CancellationToken cancellationToken)
    {
        await CheckServicesAsync(cancellationToken);

        CloudEvent? cloudEvent = ParseCloudEvent(message);
        if (cloudEvent is null)
        {
            logger.LogWarning("Ignoring queue message because it is not a valid CloudEvents event.");
            return;
        }

        if (cloudEvent.Type is not BlobCreatedEventType and not BlobDeletedEventType)
        {
            logger.LogInformation("Ignoring unsupported event type {EventType}.", cloudEvent.Type);
            return;
        }

        StorageBlobEventData? eventData = cloudEvent.Data?.ToObjectFromJson<StorageBlobEventData>();
        if (eventData is null || !blobService.TryGetBlobName(eventData.Url, out string blobName))
        {
            logger.LogWarning("Ignoring event {EventId} because its blob URL is invalid or belongs to another container.", cloudEvent.Id);
            return;
        }

        if (cloudEvent.Type == BlobCreatedEventType
            && !string.Equals(Path.GetExtension(blobName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Ignoring BlobCreated event {EventId} because blob {BlobName} is not a PDF.", cloudEvent.Id, blobName);
            return;
        }

        // Ignored events return above, so they never call Azure AI Search.
        await searchService.EnsureIndexExistsAsync(cancellationToken);

        if (cloudEvent.Type == BlobCreatedEventType)
        {
            await HandleBlobCreatedAsync(cloudEvent.Id, blobName, cancellationToken);
            return;
        }

        await HandleBlobDeletedAsync(cloudEvent.Id, blobName, cancellationToken);
    }

    private async Task CheckServicesAsync(CancellationToken cancellationToken)
    {
        if (servicesAvailable)
        {
            return;
        }

        await availabilityCheckLock.WaitAsync(cancellationToken);
        try
        {
            if (servicesAvailable)
            {
                return;
            }

            await CheckServiceAsync("Azure Blob Storage", () => blobService.CheckConfigurationAsync(cancellationToken));
            await CheckServiceAsync("Azure AI Search", () => searchService.CheckConfigurationAsync(cancellationToken));
            await CheckServiceAsync("Azure AI Document Intelligence", () => documentIntelligenceService.CheckConfigurationAsync(cancellationToken));

            // Receiving the trigger message confirms that queue access is working.
            logger.LogInformation("Azure Blob Storage, Azure AI Search, Azure AI Document Intelligence, and the trigger queue are reachable.");
            servicesAvailable = true;
        }
        finally
        {
            availabilityCheckLock.Release();
        }
    }

    private async Task CheckServiceAsync(string serviceName, Func<Task> check)
    {
        try
        {
            await check();
        }
        catch (RequestFailedException exception)
        {
            logger.LogError(exception, "Configuration check failed for {Service}. HTTP status: {Status}. {ErrorCode}", serviceName, exception.Status, exception.ErrorCode);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Configuration check failed for {Service}: {Message}", serviceName, exception.Message);
            throw;
        }
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

        // Mark the event only after every Azure operation succeeds so the queue can retry failures.
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

    private static CloudEvent? ParseCloudEvent(string message)
    {
        try
        {
            return CloudEvent.Parse(BinaryData.FromString(message));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            // A malformed message can never succeed, so it is dropped instead of retried into the poison queue.
            return null;
        }
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
