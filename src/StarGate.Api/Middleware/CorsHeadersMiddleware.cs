namespace StarGate.Api.Middleware;

/// <summary>
/// Middleware to add custom CORS-related headers.
/// </summary>
public class CorsHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorsHeadersMiddleware> _logger;

    public CorsHeadersMiddleware(
        RequestDelegate next,
        ILogger<CorsHeadersMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add correlation ID header for tracking
        if (!context.Response.Headers.ContainsKey("X-Correlation-Id"))
        {
            context.Response.Headers.Append("X-Correlation-Id", context.TraceIdentifier);
        }

        // Add request ID header
        if (!context.Response.Headers.ContainsKey("X-Request-Id"))
        {
            var requestId = Guid.NewGuid().ToString();
            context.Response.Headers.Append("X-Request-Id", requestId);
        }

        await _next(context);
    }
}
