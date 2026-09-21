using AzurePrivateEndpointDemo.API.Models;
using AzurePrivateEndpointDemo.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AzurePrivateEndpointDemo.API.Controllers;

/// <summary>
/// Exposes files and extracted page content stored in Azure AI Search.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class IndexedDocumentsController(IndexedDocumentService indexedDocumentService) : ControllerBase
{
    /// <summary>
    /// Returns the indexed files and their content, or an empty list when the index has no documents.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Files grouped by their blob identity, with text grouped by page.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<IndexedDocumentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IndexedDocumentDto>>> GetDocumentsAsync(
        CancellationToken cancellationToken)
    {
        return Ok(await indexedDocumentService.GetDocumentsAsync(cancellationToken));
    }
}
