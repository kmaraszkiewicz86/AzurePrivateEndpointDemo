using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
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
    IAzureBlobService blobService,
    IDocumentIntelligenceService documentIntelligenceService,
    IAzureSearchService searchService,
    IProcessedEventMemory processedEventMemory,
    ILogger<AzureAISearchIndexerFunction> logger)
{
    private const string BlobCreatedEventType = "Microsoft.Storage.BlobCreated";
    private const string BlobDeletedEventType = "Microsoft.Storage.BlobDeleted";
    private readonly SemaphoreSlim availabilityCheckLock = new(1, 1);
    private bool servicesAvailable;

    /// <summary>
    /// Receives one Blob Storage event from Azure Queue Storage and delegates it to the create or delete method.
    /// </summary>
    /// <param name="message">The Event Grid or CloudEvents JSON written to the queue by the Event Grid subscription.</param>
    /// <param name="functionContext">Provides the invocation ID used to distinguish queue retries in logs.</param>
    /// <param name="cancellationToken">Cancels the indexing operation.</param>
    [Function(nameof(AzureAISearchIndexerFunction))]
    public async Task RunAsync(
        [QueueTrigger("%DocumentEventsQueueName%", Connection = "DocumentEventsQueue")] string message,
        FunctionContext functionContext,
        CancellationToken cancellationToken)
    {
        QueueStorageEvent? storageEvent = null;
        string? blobName = null;
        ProcessingContext? processing = null;

        try
        {
            storageEvent = ParseStorageEvent(message);
            if (storageEvent is null)
            {
                logger.LogWarning("Ignoring queue message because it is not a valid Event Grid or CloudEvents event.");
                return;
            }

            if (storageEvent.Type is not BlobCreatedEventType and not BlobDeletedEventType)
            {
                logger.LogInformation("Ignoring event {EventId} with unsupported type {EventType}.", storageEvent.Id, storageEvent.Type);
                return;
            }

            if (storageEvent.BlobUrl is null || !blobService.TryGetBlobName(storageEvent.BlobUrl, out blobName))
            {
                logger.LogWarning("Ignoring event {EventId} of type {EventType} because its blob URL is invalid or belongs to another container.", storageEvent.Id, storageEvent.Type);
                return;
            }

            if (storageEvent.Type == BlobCreatedEventType
                && !string.Equals(Path.GetExtension(blobName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Ignoring BlobCreated event {EventId} because blob {BlobName} is not a PDF.", storageEvent.Id, blobName);
                return;
            }

            processing = new ProcessingContext(storageEvent.Id, storageEvent.Type, blobName, functionContext.InvocationId);
            logger.LogInformation(
                "Invocation {InvocationId}: accepted storage event {EventId} ({EventType}) for blob {BlobName}. Starting indexing workflow.",
                processing.InvocationId, processing.EventId, processing.EventType, processing.BlobName);

            await CheckServicesAsync(processing, cancellationToken);
            await RunStageAsync(
                processing, "EnsureSearchIndex", "Azure AI Search",
                "AzureSearchService.EnsureIndexExistsAsync (read index; create or update if needed)",
                () => searchService.EnsureIndexExistsAsync(cancellationToken));

            if (storageEvent.Type == BlobCreatedEventType)
            {
                await HandleBlobCreatedAsync(processing, cancellationToken);
            }
            else
            {
                await HandleBlobDeletedAsync(processing, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Invocation {InvocationId}, event {EventId} ({EventType}, blob {BlobName}) was canceled during stage {Stage}: {Service} / {Operation}.",
                functionContext.InvocationId, storageEvent?.Id, storageEvent?.Type, blobName,
                processing?.Stage, processing?.Service, processing?.Operation);
            throw;
        }
        catch (RequestFailedException exception)
        {
            logger.LogError(
                exception,
                "Invocation {InvocationId}, event {EventId} ({EventType}, blob {BlobName}) failed during stage {Stage}: {Service} / {Operation}. Azure HTTP {HttpStatus}, error code {ErrorCode}.",
                functionContext.InvocationId, storageEvent?.Id, storageEvent?.Type, blobName,
                processing?.Stage ?? "ValidateEvent", processing?.Service ?? "Queue message",
                processing?.Operation ?? "ParseStorageEvent", exception.Status, exception.ErrorCode);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Invocation {InvocationId}, event {EventId} ({EventType}, blob {BlobName}) failed during stage {Stage}: {Service} / {Operation}. Exception type: {ExceptionType}.",
                functionContext.InvocationId, storageEvent?.Id, storageEvent?.Type, blobName,
                processing?.Stage ?? "ValidateEvent", processing?.Service ?? "Queue message",
                processing?.Operation ?? "ParseStorageEvent", exception.GetType().Name);
            throw;
        }
    }

    private async Task CheckServicesAsync(ProcessingContext processing, CancellationToken cancellationToken)
    {
        if (servicesAvailable)
        {
            logger.LogInformation("Event {EventId}: service access checks already succeeded on this instance; skipping preflight probes.", processing.EventId);
            return;
        }

        await availabilityCheckLock.WaitAsync(cancellationToken);
        try
        {
            if (servicesAvailable)
            {
                logger.LogInformation("Event {EventId}: service access checks already succeeded on this instance; skipping preflight probes.", processing.EventId);
                return;
            }

            await RunStageAsync(
                processing, "CheckBlobStorageAccess", "Azure Blob Storage",
                "BlobContainerClient.GetPropertiesAsync",
                () => blobService.CheckConfigurationAsync(cancellationToken));
            await RunStageAsync(
                processing, "CheckSearchAccess", "Azure AI Search",
                "SearchIndexClient.GetServiceStatisticsAsync",
                () => searchService.CheckConfigurationAsync(cancellationToken));
            await RunStageAsync(
                processing, "CheckDocumentIntelligenceAccess", "Azure AI Document Intelligence",
                "DocumentIntelligenceAdministrationClient.GetResourceDetailsAsync (GET /documentintelligence/info)",
                () => documentIntelligenceService.CheckConfigurationAsync(cancellationToken));

            // Receiving the trigger message confirms that queue access is working.
            logger.LogInformation("Event {EventId}: all preflight access checks completed; queue trigger, Blob Storage, AI Search and Document Intelligence responded.", processing.EventId);
            servicesAvailable = true;
        }
        finally
        {
            availabilityCheckLock.Release();
        }
    }

    private async Task RunStageAsync(
        ProcessingContext processing,
        string stage,
        string service,
        string operation,
        Func<Task> action)
    {
        processing.SetStage(stage, service, operation);
        logger.LogInformation(
            "Invocation {InvocationId}, event {EventId} (blob {BlobName}): starting stage {Stage}: {Service} / {Operation}.",
            processing.InvocationId, processing.EventId, processing.BlobName, stage, service, operation);

        Stopwatch stopwatch = Stopwatch.StartNew();
        await action();
        logger.LogInformation(
            "Invocation {InvocationId}, event {EventId} (blob {BlobName}): completed stage {Stage}: {Service} / {Operation} in {ElapsedMs} ms.",
            processing.InvocationId, processing.EventId, processing.BlobName, stage, service, operation, stopwatch.ElapsedMilliseconds);
    }

    private async Task<T> RunStageAsync<T>(
        ProcessingContext processing,
        string stage,
        string service,
        string operation,
        Func<Task<T>> action)
    {
        processing.SetStage(stage, service, operation);
        logger.LogInformation(
            "Invocation {InvocationId}, event {EventId} (blob {BlobName}): starting stage {Stage}: {Service} / {Operation}.",
            processing.InvocationId, processing.EventId, processing.BlobName, stage, service, operation);

        Stopwatch stopwatch = Stopwatch.StartNew();
        T result = await action();
        logger.LogInformation(
            "Invocation {InvocationId}, event {EventId} (blob {BlobName}): completed stage {Stage}: {Service} / {Operation} in {ElapsedMs} ms.",
            processing.InvocationId, processing.EventId, processing.BlobName, stage, service, operation, stopwatch.ElapsedMilliseconds);
        return result;
    }

    private async Task HandleBlobCreatedAsync(ProcessingContext processing, CancellationToken cancellationToken)
    {
        if (processedEventMemory.WasProcessed(processing.EventId))
        {
            logger.LogInformation("Event {EventId} (BlobCreated, blob {BlobName}): already processed; no blob download or indexing needed.", processing.EventId, processing.BlobName);
            return;
        }

        BlobDocument blobDocument = await RunStageAsync(
            processing, "DownloadPdf", "Azure Blob Storage", "BlobClient.DownloadContentAsync",
            () => blobService.DownloadPdfAsync(processing.BlobName, cancellationToken));
        IReadOnlyList<DocumentPageChunk> pageChunks = await RunStageAsync(
            processing, "AnalyzePdf", "Azure AI Document Intelligence",
            "DocumentIntelligenceClient.AnalyzeDocumentAsync",
            () => documentIntelligenceService.ExtractPageChunksAsync(blobDocument.Content, cancellationToken));

        logger.LogInformation("Event {EventId} (blob {BlobName}): Document Intelligence returned {ChunkCount} page chunks for indexing.", processing.EventId, processing.BlobName, pageChunks.Count);
        await RunStageAsync(
            processing, "IndexDocument", "Azure AI Search",
            "AzureSearchService.IndexDocumentAsync (delete old chunks; upload new chunks)",
            () => searchService.IndexDocumentAsync(
                processing.BlobName, blobDocument.FileName, pageChunks, cancellationToken));

        // Mark the event only after every Azure operation succeeds so the queue can retry failures.
        processedEventMemory.MarkAsProcessed(processing.EventId);
        logger.LogInformation(
            "Invocation {InvocationId}, BlobCreated event {EventId} for blob {BlobName} processed successfully: indexed {ChunkCount} chunks.",
            processing.InvocationId,
            processing.EventId,
            processing.BlobName,
            pageChunks.Count);
    }

    private async Task HandleBlobDeletedAsync(ProcessingContext processing, CancellationToken cancellationToken)
    {
        if (processedEventMemory.WasProcessed(processing.EventId))
        {
            logger.LogInformation("Event {EventId} (BlobDeleted, blob {BlobName}): already processed; no search deletion needed.", processing.EventId, processing.BlobName);
            return;
        }

        bool documentExisted = await RunStageAsync(
            processing, "DeleteSearchDocument", "Azure AI Search",
            "AzureSearchService.DeleteDocumentAsync (find and delete indexed chunks)",
            () => searchService.DeleteDocumentAsync(processing.BlobName, cancellationToken));
        // Mark the event only after the search delete has completed successfully.
        processedEventMemory.MarkAsProcessed(processing.EventId);

        logger.LogInformation(
            "Invocation {InvocationId}, BlobDeleted event {EventId} for blob {BlobName} processed successfully. Document found in Azure AI Search: {DocumentExisted}.",
            processing.InvocationId,
            processing.EventId,
            processing.BlobName,
            documentExisted);
    }

    private static QueueStorageEvent? ParseStorageEvent(string message)
    {
        BinaryData payload = BinaryData.FromString(message);

        try
        {
            EventGridEvent eventGridEvent = EventGridEvent.Parse(payload);
            return new QueueStorageEvent(
                eventGridEvent.Id,
                eventGridEvent.EventType,
                GetBlobUrl(eventGridEvent.EventType, eventGridEvent.Data));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            // The message might use the CloudEvents schema instead.
        }

        try
        {
            CloudEvent? cloudEvent = CloudEvent.Parse(payload);
            return cloudEvent is null
                ? null
                : new QueueStorageEvent(
                    cloudEvent.Id,
                    cloudEvent.Type,
                    cloudEvent.Data is null ? null : GetBlobUrl(cloudEvent.Type, cloudEvent.Data));
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            // A malformed message cannot succeed, so it is dropped instead of retried into the poison queue.
            return null;
        }
    }

    private static string? GetBlobUrl(string eventType, BinaryData data) => eventType switch
    {
        BlobCreatedEventType => data.ToObjectFromJson<StorageBlobCreatedEventData>()?.Url,
        BlobDeletedEventType => data.ToObjectFromJson<StorageBlobDeletedEventData>()?.Url,
        _ => null
    };

    private sealed record QueueStorageEvent(string Id, string Type, string? BlobUrl);

    private sealed class ProcessingContext(string eventId, string eventType, string blobName, string invocationId)
    {
        public string EventId { get; } = eventId;
        public string EventType { get; } = eventType;
        public string BlobName { get; } = blobName;
        public string InvocationId { get; } = invocationId;
        public string Stage { get; private set; } = "ValidateEvent";
        public string Service { get; private set; } = "Queue message";
        public string Operation { get; private set; } = "ParseStorageEvent";

        public void SetStage(string stage, string service, string operation)
        {
            Stage = stage;
            Service = service;
            Operation = operation;
        }
    }
}
