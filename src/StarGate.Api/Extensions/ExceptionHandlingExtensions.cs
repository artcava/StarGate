namespace StarGate.Api.Extensions;

using StarGate.Api.Middleware;

/// <summary>
/// Extension methods for configuring exception handling.
/// </summary>
public static class ExceptionHandlingExtensions
{
    /// <summary>
    /// Adds global exception handling to the application.
    /// </summary>
    public static IServiceCollection AddGlobalExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandlerMiddleware>();
        services.AddProblemDetails();
        return services;
    }

    /// <summary>
    /// Uses global exception handling middleware.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(options => { }); // Use default exception handler pipeline
        return app;
    }
}
