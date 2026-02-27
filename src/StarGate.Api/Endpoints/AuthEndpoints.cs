using Microsoft.AspNetCore.Mvc;
using StarGate.Api.Models;
using StarGate.Api.Services;

namespace StarGate.Api.Endpoints;

/// <summary>
/// Authentication endpoints for development purposes.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps authentication endpoints.
    /// Only available in Development environment.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/dev-token", GenerateDevToken)
            .WithName("GenerateDevToken")
            .WithSummary("Generate a JWT token for development/testing")
            .WithDescription("Generates a valid JWT token for local development and testing. Only available in Development environment.")
            .Produces<GenerateTokenResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        return app;
    }

    private static IResult GenerateDevToken(
        [FromBody] GenerateTokenRequest request,
        [FromServices] IJwtTokenGenerator tokenGenerator,
        [FromServices] ILogger<IJwtTokenGenerator> logger)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.ClientId))
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "ClientId is required",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (request.ProcessTypes == null || request.ProcessTypes.Count == 0)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "At least one process type is required",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (request.ExpirationMinutes <= 0 || request.ExpirationMinutes > 1440) // Max 24 hours
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = "ExpirationMinutes must be between 1 and 1440 (24 hours)",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Generate token
            var token = tokenGenerator.GenerateToken(
                request.ClientId,
                request.ProcessTypes,
                request.ExpirationMinutes);

            var expiresAt = DateTime.UtcNow.AddMinutes(request.ExpirationMinutes);

            var response = new GenerateTokenResponse
            {
                Token = token,
                ExpiresAt = expiresAt,
                ClientId = request.ClientId,
                ProcessTypes = request.ProcessTypes
            };

            logger.LogInformation(
                "Development token generated for client {ClientId}",
                request.ClientId);

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating development token");
            return Results.Problem(
                title: "Token Generation Failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
