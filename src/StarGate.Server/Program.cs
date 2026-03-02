using Microsoft.Extensions.Diagnostics.HealthChecks;
using StarGate.Server.HealthChecks;
using StarGate.Server.Workers;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure host shutdown timeout
// Allow 45 seconds for graceful shutdown (30s for messages + 15s buffer)
builder.Host.ConfigureHostOptions(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(45);
});

// Register ProcessWorker as singleton to allow health check injection
builder.Services.AddSingleton<ProcessWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ProcessWorker>());

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<ProcessWorkerHealthCheck>(
        "process-worker",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "worker", "ready" });

IHost host = builder.Build();
host.Run();
