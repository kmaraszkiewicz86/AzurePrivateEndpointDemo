namespace AzureAISearchIndexer.Services;

/// <summary>
/// Tracks completed event IDs in the current Function App process.
/// </summary>
public interface IProcessedEventMemory
{
    /// <summary>
    /// Checks whether an event has already completed successfully.
    /// </summary>
    bool WasProcessed(string eventId);

    /// <summary>
    /// Records an event after its processing succeeds.
    /// </summary>
    void MarkAsProcessed(string eventId);
}
