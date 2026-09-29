namespace AzurePrivateEndpointDemo.API.Options;

/// <summary>
/// Configures which Azure identity the API uses to access Azure resources.
/// </summary>
public sealed class AzureIdentityOptions
{
    public const string SectionName = "AzureIdentity";

    /// <summary>
    /// The client ID of a user-assigned Managed Identity, or empty to use the default credential chain.
    /// </summary>
    public string? ManagedIdentityClientId { get; init; }
}
