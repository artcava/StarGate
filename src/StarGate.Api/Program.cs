using FluentValidation;
using StarGate.Api.Endpoints;
using StarGate.Api.Extensions;
using StarGate.Api.Infrastructure;
using StarGate.Api.Validators;
using StarGate.Application;
using StarGate.Core.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "StarGate API",
        Version = "v1",
        Description = "StarGate - Asynchronous Process Management System"
    });
});

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

// Map endpoints
app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .WithTags("Health")
    .WithName("HealthCheck")
    .Produces(200);

app.MapPolicyCacheEndpoints();
app.MapProcessEndpoints();

app.Run();
