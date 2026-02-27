using Microsoft.AspNetCore.Mvc;
using StarGate.Api.Models;
using StarGate.Api.Services;
using System.Security.Claims;

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

        group.MapGet("/validate", ValidateToken)
            .WithName("ValidateToken")
            .WithSummary("Validate and inspect JWT token")
            .WithDescription("Returns the decoded claims from the provided JWT token. Requires authentication.")
            .Produces<TokenValidationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

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

    private static IResult ValidateToken(
        ClaimsPrincipal user,
        [FromServices] ILogger<Program> logger)
    {
        try
        {
            var clientId = user.FindFirst("client_id")?.Value;
            var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? user.FindFirst("sub")?.Value;
            
            var processTypes = user.FindAll("process_type")
                .Select(c => c.Value)
                .ToList();

            var allClaims = user.Claims
                .Select(c => new { c.Type, c.Value })
                .ToDictionary(c => c.Type, c => c.Value);

            logger.LogInformation(
                "Token validation successful for client {ClientId}",
                clientId);

            return Results.Ok(new TokenValidationResponse
            {
                IsValid = true,
                ClientId = clientId,
                Subject = subject,
                ProcessTypes = processTypes,
                Claims = allClaims
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating token");
            return Results.Problem(
                title: "Token Validation Failed",
                detail: "An error occurred while validating the token",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}

/// <summary>
/// Response for token validation endpoint.
/// </summary>
public class TokenValidationResponse
{
    public bool IsValid { get; set; }
    public string? ClientId { get; set; }
    public string? Subject { get; set; }
    public List<string> ProcessTypes { get; set; } = new();
    public Dictionary<string, string> Claims { get; set; } = new();
}
