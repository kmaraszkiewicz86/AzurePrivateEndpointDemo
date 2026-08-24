// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

using System;
using Azure.Messaging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureAISearchIndexer;

public class AzureAISearchIndexerFunction(ILogger<AzureAISearchIndexerFunction> _logger)
{
    [Function(nameof(AzureAISearchIndexerFunction))]
    public void Run([EventGridTrigger] CloudEvent cloudEvent)
    {
        _logger.LogInformation("Event type: {type}, Event subject: {subject}", cloudEvent.Type, cloudEvent.Subject);
    }
}