using System.Text;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using AzurePrivateEndpointDemo.API.Models;
using AzurePrivateEndpointDemo.API.Options;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace AzurePrivateEndpointDemo.API.Services;

/// <summary>
/// Retrieves PDF context from Azure AI Search and creates an answer with Azure OpenAI.
/// </summary>
public sealed class ChatbotService(
    SearchClient searchClient,
    ChatClient chatClient,
    IOptions<AzureAiOptions> options)
{
    /// <summary>
    /// Answers a question using relevant chunks from the indexed PDFs.
    /// </summary>
    /// <param name="request">The user's chatbot question.</param>
    /// <param name="cancellationToken">Cancels Azure AI Search and Azure OpenAI calls.</param>
    /// <returns>The generated answer and source references.</returns>
    public async Task<ChatbotResponseDto> AskAsync(
        ChatbotRequestDto request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SearchDocumentChunk> chunks = await SearchAsync(
            request.Question,
            cancellationToken);

        // Page labels keep the retrieved text connected to the source references returned to the UI.
        string context = BuildContext(chunks);
        ChatMessage[] messages =
        [
            new SystemChatMessage(
                "Answer only from the supplied PDF context. " +
                "If the context does not contain the answer, say that the indexed documents do not provide it."),
            new UserChatMessage($"PDF context:\n{context}\n\nQuestion:\n{request.Question}")
        ];

        ChatCompletion completion = (await chatClient.CompleteChatAsync(
            messages,
            cancellationToken: cancellationToken)).Value;

        string answer = string.Concat(completion.Content.Select(part => part.Text));
        ChatSourceDto[] sources = chunks
            .DistinctBy(chunk => (chunk.DocumentId, chunk.PageNumber))
            .Select(chunk => new ChatSourceDto(
                chunk.FileName,
                chunk.PageNumber,
                chunk.DocumentId,
                $"/api/documents/{Uri.EscapeDataString(chunk.BlobName)}"))
            .ToArray();

        return new ChatbotResponseDto(answer, sources);
    }

    private async Task<IReadOnlyList<SearchDocumentChunk>> SearchAsync(
        string question,
        CancellationToken cancellationToken)
    {
        var searchOptions = new SearchOptions
        {
            Size = options.Value.SearchResultCount
        };
        searchOptions.Select.Add("documentId");
        searchOptions.Select.Add("fileName");
        searchOptions.Select.Add("pageNumber");
        searchOptions.Select.Add("content");
        searchOptions.Select.Add("blobName");

        SearchResults<SearchDocumentChunk> results = await searchClient.SearchAsync<SearchDocumentChunk>(
            question,
            searchOptions,
            cancellationToken);

        var chunks = new List<SearchDocumentChunk>();
        await foreach (SearchResult<SearchDocumentChunk> result in results.GetResultsAsync())
        {
            chunks.Add(result.Document);
        }

        return chunks;
    }

    private static string BuildContext(IReadOnlyList<SearchDocumentChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            return "No relevant PDF chunks were found.";
        }

        var context = new StringBuilder();
        for (int index = 0; index < chunks.Count; index++)
        {
            SearchDocumentChunk chunk = chunks[index];
            context.AppendLine($"[Source {index + 1}: {chunk.FileName}, page {chunk.PageNumber}]");
            context.AppendLine(chunk.Content);
            context.AppendLine();
        }

        return context.ToString();
    }
}
