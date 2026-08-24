using System.Collections.Concurrent;

namespace AzureAISearchIndexer.Services;

/// <summary>
/// Keeps processed Event Grid event IDs in the current Function App process memory.
/// </summary>
public sealed class ProcessedEventMemory
{
    private readonly ConcurrentDictionary<string, byte> _processedEventIds = new(StringComparer.Ordinal);

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
