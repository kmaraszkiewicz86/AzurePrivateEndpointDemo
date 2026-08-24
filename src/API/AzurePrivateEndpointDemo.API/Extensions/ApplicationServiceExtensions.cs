using Microsoft.Extensions.Hosting;

namespace AzurePrivateEndpointDemo.API.Extensions;

/// <summary>
/// Registers all API application services on the .NET 10 host builder.
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Adds controllers, Azure clients, application services, and CORS configuration to the API host.
    /// </summary>
    /// <param name="builder">The .NET application host builder.</param>
    /// <returns>The same builder so registrations can be chained.</returns>
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();

        builder.AddAzureStorage();
        builder.AddChatbotServices();
        builder.AddApplicationCors();

        return builder;
    }
}
