using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Search.Documents;
using AzurePrivateEndpointDemo.API.Options;
using AzurePrivateEndpointDemo.API.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace AzurePrivateEndpointDemo.API.Extensions;

/// <summary>
/// Registers Managed Identity clients used by the chatbot service.
/// </summary>
public static class ChatbotExtensions
{
    /// <summary>
    /// Adds Azure AI Search, Azure OpenAI, and the RAG chatbot service.
    /// </summary>
    /// <param name="builder">The .NET application host builder.</param>
    /// <returns>The same builder so registrations can be chained.</returns>
    public static IHostApplicationBuilder AddChatbotServices(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<AzureAiOptions>()
            .BindConfiguration(AzureAiOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Both Azure AI clients reuse the TokenCredential registered for the API managed identity.
        builder.Services.AddSingleton(serviceProvider =>
        {
            AzureAiOptions configuration = serviceProvider
                .GetRequiredService<IOptions<AzureAiOptions>>()
                .Value;
            TokenCredential credential = serviceProvider.GetRequiredService<TokenCredential>();

            return new SearchClient(
                new Uri(configuration.SearchEndpoint),
                configuration.SearchIndexName,
                credential);
        });
        builder.Services.AddSingleton(serviceProvider =>
        {
            AzureAiOptions configuration = serviceProvider
                .GetRequiredService<IOptions<AzureAiOptions>>()
                .Value;
            TokenCredential credential = serviceProvider.GetRequiredService<TokenCredential>();
            var openAIClient = new AzureOpenAIClient(
                new Uri(configuration.OpenAIEndpoint),
                credential);

            return openAIClient.GetChatClient(configuration.OpenAIChatDeployment);
        });
        builder.Services.AddSingleton<ChatbotService>();

        return builder;
    }
}
