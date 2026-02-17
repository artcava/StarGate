HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

IHost host = builder.Build();
host.Run();
