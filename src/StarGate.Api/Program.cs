using StarGate.Api.Endpoints;
using StarGate.Api.Infrastructure;
using StarGate.Application;
using StarGate.Core.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Register Infrastructure services (in-memory implementations for testing)
// TODO: Replace with actual Infrastructure layer services when available
builder.Services.AddSingleton<IPolicyRepository, InMemoryPolicyRepository>();
builder.Services.AddSingleton<ICacheStore, InMemoryCacheStore>();

// Add Application layer services
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

// Configure middleware
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));

// Map policy cache management endpoints
app.MapPolicyCacheEndpoints();

app.Run();
