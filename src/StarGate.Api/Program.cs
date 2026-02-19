using FluentValidation;
using StarGate.Api.Endpoints;
using StarGate.Api.Infrastructure;
using StarGate.Api.Validators;
using StarGate.Application;
using StarGate.Core.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Map endpoints
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));
app.MapPolicyCacheEndpoints();
app.MapProcessEndpoints();

app.Run();
