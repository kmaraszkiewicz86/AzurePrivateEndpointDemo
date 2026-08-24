namespace AzurePrivateEndpointDemo.API.Models;

/// <summary>
/// Represents a validation error returned by the API.
/// </summary>
public sealed class ErrorResponseDto
{
    /// <summary>
    /// Creates an API validation error response.
    /// </summary>
    /// <param name="error">The user-facing validation message.</param>
    public ErrorResponseDto(string error)
    {
        Error = error;
    }

    public string Error { get; }
}
