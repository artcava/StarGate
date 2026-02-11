using StarGate.Server;

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();
host.Run();
