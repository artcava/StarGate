using StarGate.Infrastructure.Extensions;
using StarGate.Core.Extensions;
using StarGate.Server.Workers;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Add services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDomain();

// Register background worker
builder.Services.AddHostedService<ProcessWorker>();

IHost host = builder.Build();
host.Run();
