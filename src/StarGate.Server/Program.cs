using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using StarGate.Core.Configuration;
using StarGate.Infrastructure.Extensions;
using StarGate.Infrastructure.Resilience;
using StarGate.Server.HealthChecks;
using StarGate.Server.Workers;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure host shutdown timeout
// Allow 45 seconds for graceful shutdown (30s for messages + 15s buffer)
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(45);
});

// Configure retry settings
builder.Services.Configure<RetryConfiguration>(
    builder.Configuration.GetSection("Retry"));

// Add resilience policies
builder.Services.AddResiliencePolicies(builder.Configuration);

// Register circuit breaker state service
builder.Services.AddSingleton<CircuitBreakerStateService>();

// Register ProcessWorker as singleton to allow health check injection
builder.Services.AddSingleton<ProcessWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ProcessWorker>());

// Register TimeoutScannerWorker for timeout enforcement
builder.Services.AddHostedService<TimeoutScannerWorker>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<ProcessWorkerHealthCheck>(
        "process-worker",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "worker", "ready" })
    .AddCheck<CircuitBreakerHealthCheck>(
        "circuit-breakers",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "resilience", "ready" });

IHost host = builder.Build();
host.Run();
