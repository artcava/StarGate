using StarGate.Api.Endpoints;
using StarGate.Application;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddApplicationServices(builder.Configuration);

// TODO: Add Infrastructure services when available
// builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Configure middleware
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));

// Map policy cache management endpoints
app.MapPolicyCacheEndpoints();

app.Run();
