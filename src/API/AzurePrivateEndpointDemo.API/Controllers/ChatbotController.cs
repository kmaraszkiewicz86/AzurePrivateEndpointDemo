using AzurePrivateEndpointDemo.API.Models;
using AzurePrivateEndpointDemo.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AzurePrivateEndpointDemo.API.Controllers;

/// <summary>
/// Exposes chatbot answers generated from indexed PDF content.
/// </summary>
[ApiController]
[Route("api/chatbot")]
public sealed class ChatbotController(ChatbotService chatbotService) : ControllerBase
{
    /// <summary>
    /// Sends a question to the chatbot service and returns its answer with source references.
    /// </summary>
    /// <param name="request">The user's question.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The grounded answer and the PDF pages used as sources.</returns>
    [HttpPost]
    [ProducesResponseType<ChatbotResponseDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatbotResponseDto>> AskChatbotAsync(
        [FromBody] ChatbotRequestDto request,
        CancellationToken cancellationToken)
    {
        ChatbotResponseDto response = await chatbotService.AskAsync(request, cancellationToken);
        return Ok(response);
    }
}
