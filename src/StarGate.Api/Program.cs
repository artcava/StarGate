using StarGate.Api.Extensions;
using StarGate.Application.Extensions;
using StarGate.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add application layers
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Add exception handling
builder.Services.AddExceptionHandling();

// Add health checks
builder.Services.AddApplicationHealthChecks(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add exception handler middleware early in pipeline
app.UseExceptionHandling();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Map health check endpoints
app.MapHealthCheckEndpoints();

app.Run();

// Make Program accessible to test projects
public partial class Program { }
