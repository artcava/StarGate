using StarGate.Api.Endpoints;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

WebApplication app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));

// Map policy cache management endpoints
app.MapPolicyCacheEndpoints();

app.Run();
