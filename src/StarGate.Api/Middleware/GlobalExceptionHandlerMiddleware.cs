using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StarGate.Api.Exceptions;
using StarGate.Core.Exceptions;
using System.Text.Json;

namespace StarGate.Api.Middleware;

/// <summary>
/// Middleware for handling unhandled exceptions globally.
/// </summary>
public class GlobalExceptionHandlerMiddleware : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlerMiddleware(
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        // Log exception with context
        LogException(exception, httpContext, traceId);

        // Create problem details response
        var includeDetails = _environment.IsDevelopment();
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            httpContext,
            includeDetails);

        // Set response
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        // Serialize and write response
        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        });

        await httpContext.Response.WriteAsync(json, cancellationToken);

        return true; // Exception handled
    }

    private void LogException(Exception exception, HttpContext httpContext, string traceId)
    {
        var requestPath = httpContext.Request.Path;
        var requestMethod = httpContext.Request.Method;

        var logLevel = exception switch
        {
            ProcessNotFoundException => LogLevel.Warning,
            DuplicateProcessException => LogLevel.Warning,
            PolicyViolationException => LogLevel.Warning,
            FluentValidation.ValidationException => LogLevel.Warning,
            DomainException => LogLevel.Warning,
            ArgumentException => LogLevel.Warning,
            OperationCanceledException => LogLevel.Information,
            _ => LogLevel.Error
        };

        _logger.Log(
            logLevel,
            exception,
            "Unhandled exception occurred. TraceId: {TraceId}, Method: {Method}, Path: {Path}, ExceptionType: {ExceptionType}",
            traceId,
            requestMethod,
            requestPath,
            exception.GetType().Name);
    }
}
