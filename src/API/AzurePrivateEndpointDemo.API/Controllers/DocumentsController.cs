using AzurePrivateEndpointDemo.API.Models;
using AzurePrivateEndpointDemo.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AzurePrivateEndpointDemo.API.Controllers;

/// <summary>
/// Exposes PDF upload, download, and delete operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class DocumentsController(DocumentService documentService) : ControllerBase
{
    /// <summary>
    /// Validates and uploads one PDF to the configured private Blob Storage container.
    /// </summary>
    /// <param name="file">The PDF sent as multipart form data.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Information required to download or delete the uploaded document.</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<DocumentUploadResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponseDto>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentUploadResponse>> UploadAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            DocumentUploadResponse response = await documentService.UploadAsync(file, cancellationToken);
            return Created(response.DownloadUrl, response);
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new ErrorResponseDto(exception.Message));
        }
    }

    /// <summary>
    /// Streams one PDF through the API without exposing a direct Blob Storage URL.
    /// </summary>
    /// <param name="blobName">The unique name of the blob.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF stream, or HTTP 404 when the blob does not exist.</returns>
    [HttpGet("{blobName}")]
    public async Task<IActionResult> DownloadAsync(string blobName, CancellationToken cancellationToken)
    {
        DownloadedDocument? document = await documentService.DownloadAsync(blobName, cancellationToken);
        return document is null
            ? NotFound()
            : File(document.Content, document.ContentType, document.FileName);
    }

    /// <summary>
    /// Deletes one PDF so Blob Storage can publish the corresponding Event Grid event.
    /// </summary>
    /// <param name="blobName">The unique name of the blob.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>HTTP 204 when deleted, or HTTP 404 when the blob does not exist.</returns>
    [HttpDelete("{blobName}")]
    public async Task<IActionResult> DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        bool deleted = await documentService.DeleteAsync(blobName, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
