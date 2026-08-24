using AzurePrivateEndpointDemo.API.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddApplicationServices();

WebApplication app = builder.Build();
app.UseApplicationPipeline();

app.Run();
