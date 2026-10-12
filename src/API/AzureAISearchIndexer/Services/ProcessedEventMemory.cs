using System.Collections.Concurrent;

namespace AzureAISearchIndexer.Services;

/// <summary>
/// Keeps processed Event Grid event IDs in the current Function App process memory.
/// </summary>
public sealed class ProcessedEventMemory : IProcessedEventMemory
{
    private readonly ConcurrentDictionary<string, byte> _processedEventIds;

    /// <summary>
    /// Uses the event ID collection shared by all invocation scopes in this process.
    /// </summary>
    /// <param name="processedEventIds">The collection created by dependency registration.</param>
    public ProcessedEventMemory(ConcurrentDictionary<string, byte> processedEventIds)
    {
        _processedEventIds = processedEventIds;
    }

    /// <summary>
    /// Checks whether the current Function process has already completed the Event Grid event.
    /// </summary>
    /// <param name="eventId">The unique Event Grid event identifier.</param>
    /// <returns><see langword="true"/> when the event was already completed.</returns>
    public bool WasProcessed(string eventId) => _processedEventIds.ContainsKey(eventId);

    /// <summary>
    /// Marks an Event Grid event as completed in the current Function process memory.
    /// </summary>
    /// <param name="eventId">The unique Event Grid event identifier.</param>
    public void MarkAsProcessed(string eventId) => _processedEventIds.TryAdd(eventId, 0);
}
