using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using StarGate.Api.Models;
using StarGate.Contracts.Requests;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Exceptions;

namespace StarGate.Api.Endpoints;

/// <summary>
/// API endpoints for process management.
/// </summary>
public static class ProcessEndpoints
{
    public static void MapProcessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/processes")
            .WithTags("Processes");

        // POST /api/processes - Create a new process
        group.MapPost("/", CreateProcessAsync)
            .WithName("CreateProcess")
            .Produces<ProcessResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests)
            .Produces(StatusCodes.Status500InternalServerError);

        // GET /api/processes/{processId} - Get process by ID
        group.MapGet("/{processId:guid}", GetProcessByIdAsync)
            .WithName("GetProcessById")
            .Produces<ProcessResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

        // GET /api/processes/client/{clientId}/{clientProcessId} - Get process by client identifiers
        group.MapGet("/client/{clientId}/{clientProcessId}", GetProcessByClientIdAsync)
            .WithName("GetProcessByClientId")
            .Produces<ProcessResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> CreateProcessAsync(
        [FromBody] CreateProcessRequest request,
        [FromServices] IValidator<CreateProcessRequest> validator,
        [FromServices] IProcessService processService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // Validate request
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            logger.LogWarning(
                "CreateProcess validation failed: {Errors}",
                string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));

            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        try
        {
            logger.LogInformation(
                "Creating process: ClientId={ClientId}, ProcessType={ProcessType}, ClientProcessId={ClientProcessId}",
                request.ClientId,
                request.ProcessType,
                request.ClientProcessId);

            // Check if process already exists (idempotency)
            var existingProcess = await processService.GetProcessByClientProcessIdAsync(
                request.ClientId,
                request.ClientProcessId,
                cancellationToken);

            if (existingProcess != null)
            {
                logger.LogWarning(
                    "Duplicate process detected: ClientId={ClientId}, ClientProcessId={ClientProcessId}",
                    request.ClientId,
                    request.ClientProcessId);

                return Results.Problem(
                    title: "Duplicate Process",
                    detail: $"A process with ClientProcessId '{request.ClientProcessId}' already exists for this client",
                    statusCode: StatusCodes.Status409Conflict);
            }

            // Map CreateProcessRequest to SubmitProcessRequest (record constructor)
            var submitRequest = new SubmitProcessRequest(
                ClientProcessId: request.ClientProcessId,
                ProcessType: request.ProcessType,
                Payload: request.Metadata ?? new Dictionary<string, string>(),
                IdempotencyKey: request.IdempotencyKey
            );

            var process = await processService.SubmitProcessAsync(
                request.ClientId,
                submitRequest,
                cancellationToken);

            var response = ProcessResponse.FromDomain(process);

            logger.LogInformation(
                "Process created successfully: ProcessId={ProcessId}",
                process.ProcessId);

            return Results.Created($"/api/processes/{process.ProcessId}", response);
        }
        catch (PolicyViolationException ex)
        {
            logger.LogWarning(
                ex,
                "Policy violation for ClientId={ClientId}, ProcessType={ProcessType}",
                request.ClientId,
                request.ProcessType);

            return Results.Problem(
                title: "Policy Violation",
                detail: ex.Message,
                statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error creating process: ClientId={ClientId}, ProcessType={ProcessType}",
                request.ClientId,
                request.ProcessType);

            return Results.Problem(
                title: "Internal Server Error",
                detail: "An unexpected error occurred while creating the process",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetProcessByIdAsync(
        Guid processId,
        [FromServices] IProcessService processService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Retrieving process: ProcessId={ProcessId}", processId);

            var process = await processService.GetProcessByIdAsync(processId, cancellationToken);
            
            if (process == null)
            {
                logger.LogWarning("Process not found: ProcessId={ProcessId}", processId);

                return Results.Problem(
                    title: "Process Not Found",
                    detail: $"Process with ID '{processId}' not found",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var response = ProcessResponse.FromDomain(process);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error retrieving process: ProcessId={ProcessId}",
                processId);

            return Results.Problem(
                title: "Internal Server Error",
                detail: "An unexpected error occurred while retrieving the process",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetProcessByClientIdAsync(
        string clientId,
        string clientProcessId,
        [FromServices] IProcessService processService,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug(
                "Retrieving process: ClientId={ClientId}, ClientProcessId={ClientProcessId}",
                clientId,
                clientProcessId);

            var process = await processService.GetProcessByClientProcessIdAsync(
                clientId,
                clientProcessId,
                cancellationToken);

            if (process == null)
            {
                logger.LogWarning(
                    "Process not found: ClientId={ClientId}, ClientProcessId={ClientProcessId}",
                    clientId,
                    clientProcessId);

                return Results.Problem(
                    title: "Process Not Found",
                    detail: $"Process with ClientId '{clientId}' and ClientProcessId '{clientProcessId}' not found",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var response = ProcessResponse.FromDomain(process);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error retrieving process: ClientId={ClientId}, ClientProcessId={ClientProcessId}",
                clientId,
                clientProcessId);

            return Results.Problem(
                title: "Internal Server Error",
                detail: "An unexpected error occurred while retrieving the process",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
