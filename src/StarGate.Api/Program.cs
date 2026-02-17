WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

WebApplication app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));

app.Run();
