using StarGate.Server.Workers;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Register background worker
builder.Services.AddHostedService<ProcessWorker>();

IHost host = builder.Build();
host.Run();
