namespace AzurePrivateEndpointDemo.API.Extensions;

/// <summary>
/// Configures the ASP.NET Core request pipeline.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures exception handling, HTTPS redirection, CORS, and controller endpoints.
    /// </summary>
    /// <param name="app">The built ASP.NET Core application.</param>
    /// <returns>The same application instance.</returns>
    public static WebApplication UseApplicationPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseCors();
        app.MapControllers();

        return app;
    }
}
