using Microsoft.Extensions.Hosting;

namespace AzurePrivateEndpointDemo.API.Extensions;

/// <summary>
/// Registers the configured React development origins.
/// </summary>
public static class CorsExtensions
{
    /// <summary>
    /// Adds the CORS policy that allows configured React application origins.
    /// </summary>
    /// <param name="builder">The .NET application host builder.</param>
    /// <returns>The same builder so registrations can be chained.</returns>
    public static IHostApplicationBuilder AddApplicationCors(this IHostApplicationBuilder builder)
    {
        string[] allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            }
        }));

        return builder;
    }
}
