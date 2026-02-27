using FluentValidation;
using StarGate.Api.Endpoints;
using StarGate.Api.Extensions;
using StarGate.Api.Infrastructure;
using StarGate.Api.Middleware;
using StarGate.Api.Services;
using StarGate.Api.Validators;
using StarGate.Application;
using StarGate.Core.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt(); // Updated to include JWT

// Add CORS
builder.Services.AddApiCors(builder.Configuration, builder.Environment);

// Add authentication
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// Add rate limiting
builder.Services.AddApiRateLimiting(builder.Configuration);

// Add global exception handling
builder.Services.AddGlobalExceptionHandling();

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateProcessRequestValidator>();

// Register Infrastructure services (in-memory implementations for testing)
// TODO: Replace with actual Infrastructure layer services when available
builder.Services.AddSingleton<IPolicyRepository, InMemoryPolicyRepository>();
builder.Services.AddSingleton<ICacheStore, InMemoryCacheStore>();

// Add Application layer services
builder.Services.AddApplicationServices(builder.Configuration);

// Add health checks
builder.Services.AddApplicationHealthChecks(builder.Configuration);

// Add JWT token generator for development (only in Development environment)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
}

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "StarGate API v1");
        options.RoutePrefix = "swagger";
    });
}

// Use global exception handling (must be early in pipeline)
app.UseGlobalExceptionHandling();

app.UseHttpsRedirection();

// Use CORS (must be before authentication and authorization)
app.UseApiCors();

// Add CORS headers middleware (for custom correlation headers)
app.UseMiddleware<CorsHeadersMiddleware>();

// Use rate limiting (before authentication to protect against abuse)
app.UseRateLimiter();

// Authentication & Authorization (order matters!)
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

// Rate limiting test endpoint (for testing purposes)
app.MapGet("/ratelimit-test", () => Results.Ok(new { message = "Rate limit test endpoint" }))
    .WithName("RateLimitTest")
    .RequireRateLimiting("ReadProcess")
    .ExcludeFromDescription();

app.MapPolicyCacheEndpoints();
app.MapProcessEndpoints();

// Map authentication endpoints (Development only)
if (app.Environment.IsDevelopment())
{
    app.MapAuthEndpoints();
}

// Map health check endpoints (before authentication)
app.MapHealthCheckEndpoints();

app.Run();

// Make Program accessible to test projects
public partial class Program { }
