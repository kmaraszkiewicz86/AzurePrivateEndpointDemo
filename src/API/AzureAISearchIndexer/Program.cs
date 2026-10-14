using AzureAISearchIndexer.Services;
using Microsoft.ApplicationInsights.WorkerService;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService(new ApplicationInsightsServiceOptions
    {
        EnableAdaptiveSampling = false
    })
    .ConfigureFunctionsApplicationInsights();

// The Application Insights worker provider defaults to Warning; include success logs at Information level.
builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("AzureAISearchIndexer", LogLevel.Information);

builder.AddIndexingServices();

builder.Build().Run();
