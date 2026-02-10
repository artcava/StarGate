# Client Technical Analysis - StarGate SDK

**Document Version:** 1.0  
**Last Updated:** 2026-02-10  
**Status:** Draft - Future Implementation

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Client SDK Architecture](#client-sdk-architecture)
3. [SDK Implementation](#sdk-implementation)
4. [Adaptive Polling Strategy](#adaptive-polling-strategy)
5. [Authentication Integration](#authentication-integration)
6. [Offline Queue Management](#offline-queue-management)
7. [Error Handling & Resilience](#error-handling--resilience)
8. [Usage Examples](#usage-examples)
9. [Testing Strategy](#testing-strategy)
10. [Development Roadmap](#development-roadmap)

---

## Executive Summary

Questo documento descrive l'analisi tecnica e il design del **StarGate Client SDK**, una libreria .NET che semplificherà l'integrazione con l'API StarGate per le applicazioni client.

### Obiettivi del SDK

- **Semplificare l'integrazione** con l'API StarGate
- **Gestire automaticamente** l'autenticazione OAuth2
- **Implementare polling adattivo** per monitorare lo stato dei processi
- **Fornire una coda offline** per gestire scenari di rete instabile
- **Garantire resilienza** con retry automatici e circuit breaker

### Scope

**IN SCOPE:**
- Libreria client .NET 8
- Gestione autenticazione OAuth2
- Polling adattivo per stato processi
- Coda offline per submission fallite
- Pattern di resilienza (retry, circuit breaker)
- Unit e integration testing

**OUT OF SCOPE:**
- Integrazione con applicazioni client specifiche
- UI/UX delle applicazioni client
- Configurazione Identity Provider
- Deployment delle applicazioni client

---

## Client SDK Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Client Application                                         │
│                                                             │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  StarGate.Client SDK                                   │ │
│  │                                                        │ │
│  │  ┌──────────────┐      ┌──────────────┐              │ │
│  │  │StarGateClient│──────│TokenProvider │              │ │
│  │  │              │      │(OAuth2)      │              │ │
│  │  └──────┬───────┘      └──────────────┘              │ │
│  │         │                                             │ │
│  │         │    ┌─────────────────┐                     │ │
│  │         ├────│ProcessPoller    │                     │ │
│  │         │    │(Adaptive Logic) │                     │ │
│  │         │    └─────────────────┘                     │ │
│  │         │                                             │ │
│  │         │    ┌─────────────────┐                     │ │
│  │         └────│OfflineQueue     │                     │ │
│  │              │(File-based)     │                     │ │
│  │              └─────────────────┘                     │ │
│  └────────────────────────────────────────────────────────┘ │
│                          │                                  │
│                          │ HTTPS                            │
│                          ▼                                  │
│              ┌─────────────────────┐                        │
│              │  StarGate.Api       │                        │
│              │  (Gateway)          │                        │
│              └─────────────────────┘                        │
└─────────────────────────────────────────────────────────────┘
```

### Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Runtime | .NET | 8.0 |
| HTTP Client | System.Net.Http | Built-in |
| Authentication | IdentityModel | 6.x |
| Resilience | Polly | 8.x |
| Logging | Microsoft.Extensions.Logging | 8.0 |
| Serialization | System.Text.Json | Built-in |

---

## SDK Implementation

### Project Structure

```
StarGate.Client/
├── StarGateClient.cs                # Main client class
├── StarGateClientOptions.cs         # Configuration
├── Models/
│   ├── ProcessSubmissionResult.cs
│   └── ClientProcessStatus.cs
├── Auth/
│   ├── ITokenProvider.cs
│   └── OAuth2TokenProvider.cs
├── Polling/
│   └── ProcessPoller.cs
├── Queue/
│   ├── IOfflineQueue.cs
│   └── FileBasedOfflineQueue.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

### Core Client Interface

```csharp
namespace StarGate.Client;

/// <summary>
/// Client SDK per l'integrazione con StarGate API.
/// </summary>
public interface IStarGateClient
{
    /// <summary>
    /// Sottomette un nuovo processo all'API StarGate.
    /// </summary>
    /// <typeparam name="TData">Tipo dei dati del processo.</typeparam>
    /// <param name="clientProcessId">Identificatore univoco fornito dal client.</param>
    /// <param name="processType">Tipo di processo (es. "order", "shipping").</param>
    /// <param name="data">Dati del processo.</param>
    /// <param name="ct">Token di cancellazione.</param>
    /// <returns>Risultato della submission con ID del processo.</returns>
    Task<ProcessSubmissionResult> SubmitProcessAsync<TData>(
        string clientProcessId,
        string processType,
        TData data,
        CancellationToken ct = default);

    /// <summary>
    /// Recupera lo stato corrente di un processo.
    /// </summary>
    /// <param name="processId">ID del processo (GUID).</param>
    /// <param name="ct">Token di cancellazione.</param>
    /// <returns>Stato del processo o null se non trovato.</returns>
    Task<Process?> GetProcessStatusAsync(
        Guid processId,
        CancellationToken ct = default);

    /// <summary>
    /// Attende il completamento di un processo utilizzando polling adattivo.
    /// </summary>
    /// <param name="processId">ID del processo (GUID).</param>
    /// <param name="ct">Token di cancellazione.</param>
    /// <returns>Processo completato con risultato o errore.</returns>
    Task<Process> WaitForCompletionAsync(
        Guid processId,
        CancellationToken ct = default);
}
```

### StarGate Client Implementation

```csharp
namespace StarGate.Client;

public class StarGateClient : IStarGateClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;
    private readonly IOfflineQueue _offlineQueue;
    private readonly ProcessPoller _poller;
    private readonly ILogger<StarGateClient> _logger;

    public StarGateClient(
        HttpClient httpClient,
        ITokenProvider tokenProvider,
        IOfflineQueue offlineQueue,
        ILogger<StarGateClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _offlineQueue = offlineQueue ?? throw new ArgumentNullException(nameof(offlineQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _poller = new ProcessPoller(this, logger);
    }

    public async Task<ProcessSubmissionResult> SubmitProcessAsync<TData>(
        string clientProcessId,
        string processType,
        TData data,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientProcessId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processType);
        ArgumentNullException.ThrowIfNull(data);

        var request = new SubmitProcessRequest(
            clientProcessId,
            processType,
            data,
            Guid.NewGuid().ToString()); // Generate idempotency key

        try
        {
            // Ottieni token OAuth per questo tipo di processo
            var token = await _tokenProvider.GetTokenAsync(processType, ct);

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            // Sottometti la richiesta
            var response = await _httpClient.PostAsJsonAsync(
                "/api/stargate/processes",
                request,
                ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning(
                    "Rate limit exceeded for process submission");
                throw new StarGateException(
                    "Rate limit exceeded. Please try again later.");
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<SubmitProcessResponse>(ct);

            if (result is null)
            {
                throw new StarGateException(
                    "Invalid response from server");
            }

            _logger.LogInformation(
                "Process {ProcessId} submitted successfully",
                result.ProcessId);

            return new ProcessSubmissionResult(
                result.ProcessId,
                result.ClientProcessId,
                result.Status);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to submit process, enqueueing offline");

            // Accoda per retry successivo
            await _offlineQueue.EnqueueAsync(request, ct);

            return new ProcessSubmissionResult(
                null,
                clientProcessId,
                "queued_offline");
        }
    }

    public async Task<Process?> GetProcessStatusAsync(
        Guid processId,
        CancellationToken ct = default)
    {
        if (processId == Guid.Empty)
        {
            throw new ArgumentException(
                "Process ID cannot be empty",
                nameof(processId));
        }

        try
        {
            var token = await _tokenProvider.GetTokenAsync(ct: ct);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync(
                $"/api/stargate/processes/{processId}",
                ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Process {ProcessId} not found",
                    processId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<Process>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving process status for {ProcessId}",
                processId);
            throw new StarGateException(
                $"Failed to retrieve process status: {ex.Message}",
                ex);
        }
    }

    public async Task<Process> WaitForCompletionAsync(
        Guid processId,
        CancellationToken ct = default)
    {
        if (processId == Guid.Empty)
        {
            throw new ArgumentException(
                "Process ID cannot be empty",
                nameof(processId));
        }

        return await _poller.WaitForCompletionAsync(processId, ct);
    }
}
```

### Configuration Options

```csharp
namespace StarGate.Client;

/// <summary>
/// Opzioni di configurazione per StarGateClient.
/// </summary>
public class StarGateClientOptions
{
    /// <summary>
    /// Base URL dell'API StarGate.
    /// </summary>
    public required string ApiBaseUrl { get; init; }

    /// <summary>
    /// Client ID per OAuth2 authentication.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Client Secret per OAuth2 authentication.
    /// </summary>
    public required string ClientSecret { get; init; }

    /// <summary>
    /// Authority URL del provider OAuth2.
    /// </summary>
    public required string Authority { get; init; }

    /// <summary>
    /// Scope richiesti per l'autenticazione.
    /// </summary>
    public List<string> Scopes { get; init; } = new();

    /// <summary>
    /// Timeout per le richieste HTTP (default: 30 secondi).
    /// </summary>
    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Percorso per la coda offline (default: "./offline-queue").
    /// </summary>
    public string OfflineQueuePath { get; init; } = "./offline-queue";

    /// <summary>
    /// Abilita la coda offline (default: true).
    /// </summary>
    public bool EnableOfflineQueue { get; init; } = true;
}
```

---

## Adaptive Polling Strategy

### Strategia di Polling

Il client SDK implementa una strategia di polling adattivo che bilancia reattività e carico sul server:

**Fase 1 - Aggressive (primi 2 minuti):**
- Intervallo: 30 secondi
- Ideale per processi rapidi

**Fase 2 - Conservative (dopo 2 minuti):**
- Intervallo: 60 secondi
- Riduce il carico per processi più lunghi

**Timeout:**
- Warning dopo 10 minuti
- Il client può decidere di continuare o annullare

### Implementation

```csharp
namespace StarGate.Client.Polling;

public class ProcessPoller
{
    private readonly IStarGateClient _client;
    private readonly ILogger _logger;

    // Costanti per la strategia di polling
    private const int Phase1DurationMinutes = 2;
    private const int Phase1IntervalSeconds = 30;
    private const int Phase2IntervalSeconds = 60;
    private const int TimeoutMinutes = 10;

    public ProcessPoller(IStarGateClient client, ILogger logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Attende il completamento di un processo con polling adattivo.
    /// </summary>
    /// <param name="processId">ID del processo da monitorare.</param>
    /// <param name="ct">Token di cancellazione.</param>
    /// <returns>Processo completato.</returns>
    /// <exception cref="OperationCanceledException">Se l'operazione viene cancellata.</exception>
    /// <exception cref="StarGateException">Se il processo non viene trovato.</exception>
    public async Task<Process> WaitForCompletionAsync(
        Guid processId,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Starting adaptive polling for process {ProcessId}",
            processId);

        while (!ct.IsCancellationRequested)
        {
            // Poll per lo stato corrente
            var process = await _client.GetProcessStatusAsync(processId, ct);

            if (process is null)
            {
                throw new StarGateException(
                    $"Process {processId} not found");
            }

            // Verifica se completato
            if (process.Status is ProcessStatus.Completed or ProcessStatus.Failed)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Process {ProcessId} completed with status {Status} after {Duration}",
                    processId,
                    process.Status,
                    duration);

                return process;
            }

            // Calcola tempo trascorso
            var elapsed = DateTime.UtcNow - startTime;
            var elapsedMinutes = elapsed.TotalMinutes;

            // Delay adattivo
            TimeSpan delay;
            if (elapsedMinutes < Phase1DurationMinutes)
            {
                // Fase 1: Polling aggressivo (30s)
                delay = TimeSpan.FromSeconds(Phase1IntervalSeconds);
            }
            else
            {
                // Fase 2: Polling conservativo (60s)
                delay = TimeSpan.FromSeconds(Phase2IntervalSeconds);
            }

            _logger.LogDebug(
                "Process {ProcessId} at {Progress}% ({Status}), waiting {Delay}s",
                processId,
                process.Progress,
                process.Status,
                delay.TotalSeconds);

            await Task.Delay(delay, ct);

            // Warning se supera il timeout
            if (elapsedMinutes > TimeoutMinutes)
            {
                _logger.LogWarning(
                    "Process {ProcessId} exceeded timeout ({Timeout} minutes)",
                    processId,
                    TimeoutMinutes);
            }
        }

        throw new OperationCanceledException();
    }
}
```

### Polling Metrics

Il poller può essere esteso per raccogliere metriche:

```csharp
public class PollingMetrics
{
    public int TotalPolls { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AveragePollInterval { get; set; }
    public int Phase1Polls { get; set; }
    public int Phase2Polls { get; set; }
}
```

---

## Authentication Integration

### Token Provider Interface

```csharp
namespace StarGate.Client.Auth;

/// <summary>
/// Provider per l'acquisizione e gestione dei token OAuth2.
/// </summary>
public interface ITokenProvider
{
    /// <summary>
    /// Ottiene un token di accesso valido.
    /// </summary>
    /// <param name="processType">Tipo di processo per cui richiedere lo scope (opzionale).</param>
    /// <param name="ct">Token di cancellazione.</param>
    /// <returns>Token di accesso JWT.</returns>
    Task<string> GetTokenAsync(string? processType = null, CancellationToken ct = default);

    /// <summary>
    /// Invalida il token corrente forzando un refresh.
    /// </summary>
    Task InvalidateTokenAsync();
}
```

### OAuth2 Token Provider

```csharp
namespace StarGate.Client.Auth;

using IdentityModel.Client;

/// <summary>
/// Implementazione OAuth2 Client Credentials flow per StarGate.
/// </summary>
public class OAuth2TokenProvider : ITokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly StarGateClientOptions _options;
    private readonly ILogger<OAuth2TokenProvider> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string? _cachedToken;
    private DateTime _tokenExpiration = DateTime.MinValue;

    public OAuth2TokenProvider(
        HttpClient httpClient,
        StarGateClientOptions options,
        ILogger<OAuth2TokenProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GetTokenAsync(
        string? processType = null,
        CancellationToken ct = default)
    {
        await _tokenLock.WaitAsync(ct);

        try
        {
            // Verifica se il token è ancora valido (con buffer di 5 minuti)
            if (_cachedToken is not null &&
                DateTime.UtcNow < _tokenExpiration.AddMinutes(-5))
            {
                _logger.LogDebug("Using cached token");
                return _cachedToken;
            }

            _logger.LogInformation("Requesting new access token");

            // Prepara gli scope
            var scopes = new List<string>(_options.Scopes);
            if (!string.IsNullOrWhiteSpace(processType))
            {
                scopes.Add($"stargate:process:{processType}");
            }

            // Richiedi nuovo token
            var tokenResponse = await _httpClient.RequestClientCredentialsTokenAsync(
                new ClientCredentialsTokenRequest
                {
                    Address = $"{_options.Authority}/connect/token",
                    ClientId = _options.ClientId,
                    ClientSecret = _options.ClientSecret,
                    Scope = string.Join(" ", scopes)
                },
                ct);

            if (tokenResponse.IsError)
            {
                _logger.LogError(
                    "Token request failed: {Error} - {ErrorDescription}",
                    tokenResponse.Error,
                    tokenResponse.ErrorDescription);

                throw new StarGateException(
                    $"Failed to acquire access token: {tokenResponse.Error}");
            }

            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            _logger.LogInformation(
                "Access token acquired, expires at {Expiration}",
                _tokenExpiration);

            return _cachedToken!;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public Task InvalidateTokenAsync()
    {
        _logger.LogInformation("Invalidating cached token");
        _cachedToken = null;
        _tokenExpiration = DateTime.MinValue;
        return Task.CompletedTask;
    }
}
```

---

## Offline Queue Management

### Offline Queue Interface

```csharp
namespace StarGate.Client.Queue;

/// <summary>
/// Coda offline per gestire submission fallite.
/// </summary>
public interface IOfflineQueue
{
    /// <summary>
    /// Accoda una richiesta fallita per retry successivo.
    /// </summary>
    Task EnqueueAsync<T>(T request, CancellationToken ct = default);

    /// <summary>
    /// Recupera tutte le richieste in coda.
    /// </summary>
    Task<IEnumerable<QueuedRequest>> GetPendingRequestsAsync(CancellationToken ct = default);

    /// <summary>
    /// Rimuove una richiesta dalla coda dopo successo.
    /// </summary>
    Task DequeueAsync(string requestId, CancellationToken ct = default);

    /// <summary>
    /// Svuota la coda ritentando tutte le richieste.
    /// </summary>
    Task<int> FlushQueueAsync(CancellationToken ct = default);
}
```

### File-Based Implementation

```csharp
namespace StarGate.Client.Queue;

/// <summary>
/// Implementazione file-based della coda offline.
/// </summary>
public class FileBasedOfflineQueue : IOfflineQueue
{
    private readonly string _queueDirectory;
    private readonly ILogger<FileBasedOfflineQueue> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public FileBasedOfflineQueue(
        StarGateClientOptions options,
        ILogger<FileBasedOfflineQueue> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _queueDirectory = options.OfflineQueuePath;

        // Crea directory se non existe
        if (!Directory.Exists(_queueDirectory))
        {
            Directory.CreateDirectory(_queueDirectory);
            _logger.LogInformation(
                "Created offline queue directory: {Directory}",
                _queueDirectory);
        }
    }

    public async Task EnqueueAsync<T>(T request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _fileLock.WaitAsync(ct);

        try
        {
            var requestId = Guid.NewGuid().ToString("N");
            var fileName = Path.Combine(_queueDirectory, $"{requestId}.json");

            var queuedRequest = new QueuedRequest
            {
                Id = requestId,
                Request = request,
                EnqueuedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            var json = JsonSerializer.Serialize(queuedRequest);
            await File.WriteAllTextAsync(fileName, json, ct);

            _logger.LogInformation(
                "Enqueued request {RequestId} to offline queue",
                requestId);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<IEnumerable<QueuedRequest>> GetPendingRequestsAsync(
        CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct);

        try
        {
            var files = Directory.GetFiles(_queueDirectory, "*.json");
            var requests = new List<QueuedRequest>();

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var request = JsonSerializer.Deserialize<QueuedRequest>(json);

                    if (request is not null)
                    {
                        requests.Add(request);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error reading queued request from {File}",
                        file);
                }
            }

            _logger.LogDebug(
                "Found {Count} pending requests in offline queue",
                requests.Count);

            return requests;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DequeueAsync(string requestId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        await _fileLock.WaitAsync(ct);

        try
        {
            var fileName = Path.Combine(_queueDirectory, $"{requestId}.json");

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
                _logger.LogInformation(
                    "Dequeued request {RequestId} from offline queue",
                    requestId);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<int> FlushQueueAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting offline queue flush");

        var pendingRequests = await GetPendingRequestsAsync(ct);
        var successCount = 0;

        foreach (var queuedRequest in pendingRequests)
        {
            try
            {
                // Qui andrebbero ritentate le submission
                // Per ora semplicemente rimuove dalla coda
                await DequeueAsync(queuedRequest.Id, ct);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error flushing request {RequestId}",
                    queuedRequest.Id);
            }
        }

        _logger.LogInformation(
            "Flushed {Success}/{Total} requests from offline queue",
            successCount,
            pendingRequests.Count());

        return successCount;
    }
}

public class QueuedRequest
{
    public required string Id { get; init; }
    public required object Request { get; init; }
    public required DateTime EnqueuedAt { get; init; }
    public required int RetryCount { get; init; }
}
```

---

## Error Handling & Resilience

### Custom Exceptions

```csharp
namespace StarGate.Client;

/// <summary>
/// Eccezione base per errori del client StarGate.
/// </summary>
public class StarGateException : Exception
{
    public StarGateException(string message) : base(message) { }

    public StarGateException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Eccezione per errori di autenticazione.
/// </summary>
public class StarGateAuthenticationException : StarGateException
{
    public StarGateAuthenticationException(string message) : base(message) { }
}

/// <summary>
/// Eccezione per rate limiting.
/// </summary>
public class StarGateRateLimitException : StarGateException
{
    public TimeSpan? RetryAfter { get; }

    public StarGateRateLimitException(string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        RetryAfter = retryAfter;
    }
}
```

### Polly Integration

```csharp
namespace StarGate.Client.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStarGateClient(
        this IServiceCollection services,
        Action<StarGateClientOptions> configureOptions)
    {
        var options = new StarGateClientOptions();
        configureOptions(options);

        // Registra opzioni
        services.AddSingleton(options);

        // Registra HttpClient con Polly policies
        services.AddHttpClient<IStarGateClient, StarGateClient>(client =>
        {
            client.BaseAddress = new Uri(options.ApiBaseUrl);
            client.Timeout = options.HttpTimeout;
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Registra dipendenze
        services.AddSingleton<ITokenProvider, OAuth2TokenProvider>();

        if (options.EnableOfflineQueue)
        {
            services.AddSingleton<IOfflineQueue, FileBasedOfflineQueue>();
        }

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    // Log retry
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}
```

---

## Usage Examples

### Basic Setup

```csharp
// Configurazione in Program.cs
builder.Services.AddStarGateClient(options =>
{
    options.ApiBaseUrl = "https://api.stargate.example.com";
    options.ClientId = "my-client-id";
    options.ClientSecret = "my-client-secret";
    options.Authority = "https://auth.example.com";
    options.Scopes = new List<string> { "stargate:process:*" };
    options.EnableOfflineQueue = true;
    options.OfflineQueuePath = "./offline-queue";
});
```

### Submit and Wait for Completion

```csharp
public class OrderService
{
    private readonly IStarGateClient _starGateClient;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IStarGateClient starGateClient,
        ILogger<OrderService> logger)
    {
        _starGateClient = starGateClient;
        _logger = logger;
    }

    public async Task<string> ProcessOrderAsync(OrderData orderData)
    {
        try
        {
            // Sottometti il processo
            var submission = await _starGateClient.SubmitProcessAsync(
                clientProcessId: $"order-{orderData.OrderId}",
                processType: "order",
                data: orderData);

            if (submission.ProcessId is null)
            {
                _logger.LogWarning(
                    "Order {OrderId} queued offline",
                    orderData.OrderId);
                return "queued";
            }

            _logger.LogInformation(
                "Order {OrderId} submitted as process {ProcessId}",
                orderData.OrderId,
                submission.ProcessId);

            // Attendi completamento con polling adattivo
            var completedProcess = await _starGateClient.WaitForCompletionAsync(
                submission.ProcessId.Value);

            if (completedProcess.Status == ProcessStatus.Completed)
            {
                _logger.LogInformation(
                    "Order {OrderId} completed successfully",
                    orderData.OrderId);

                // Estrai risultato
                var result = JsonSerializer.Deserialize<OrderResult>(
                    completedProcess.Result!.ToString()!);

                return result!.TrackingNumber;
            }
            else
            {
                _logger.LogError(
                    "Order {OrderId} failed: {Error}",
                    orderData.OrderId,
                    completedProcess.Error?.Message);

                throw new InvalidOperationException(
                    completedProcess.Error?.Message ?? "Unknown error");
            }
        }
        catch (StarGateException ex)
        {
            _logger.LogError(ex, "Error processing order {OrderId}", orderData.OrderId);
            throw;
        }
    }
}
```

### Check Status Only

```csharp
public async Task<string> GetOrderStatusAsync(Guid processId)
{
    var process = await _starGateClient.GetProcessStatusAsync(processId);

    if (process is null)
    {
        return "not_found";
    }

    return process.Status switch
    {
        ProcessStatus.Accepted => $"Accepted - {process.Progress}%",
        ProcessStatus.Processing => $"Processing - {process.CurrentStep}",
        ProcessStatus.Completed => "Completed",
        ProcessStatus.Failed => $"Failed - {process.Error?.Message}",
        _ => "Unknown"
    };
}
```

---

## Testing Strategy

### Unit Tests

```csharp
namespace StarGate.Client.Tests;

public class StarGateClientTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly Mock<ITokenProvider> _tokenProviderMock;
    private readonly Mock<IOfflineQueue> _offlineQueueMock;
    private readonly StarGateClient _sut;

    public StarGateClientTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _tokenProviderMock = new Mock<ITokenProvider>();
        _offlineQueueMock = new Mock<IOfflineQueue>();

        var httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.test.com")
        };

        _sut = new StarGateClient(
            httpClient,
            _tokenProviderMock.Object,
            _offlineQueueMock.Object,
            Mock.Of<ILogger<StarGateClient>>());
    }

    [Fact]
    public async Task SubmitProcessAsync_ShouldReturnProcessId_WhenSuccessful()
    {
        // Arrange
        var expectedResponse = new SubmitProcessResponse(
            Guid.NewGuid(),
            "client-123",
            "order",
            "accepted",
            "/api/stargate/processes/...",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(5));

        _tokenProviderMock
            .Setup(x => x.GetTokenAsync(It.IsAny<string>(), default))
            .ReturnsAsync("fake-token");

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Accepted,
                Content = JsonContent.Create(expectedResponse)
            });

        // Act
        var result = await _sut.SubmitProcessAsync(
            "client-123",
            "order",
            new { orderId = "ORD-001" });

        // Assert
        result.Should().NotBeNull();
        result.ProcessId.Should().Be(expectedResponse.ProcessId);
        result.Status.Should().Be("accepted");
    }

    [Fact]
    public async Task SubmitProcessAsync_ShouldEnqueueOffline_WhenHttpFails()
    {
        // Arrange
        _tokenProviderMock
            .Setup(x => x.GetTokenAsync(It.IsAny<string>(), default))
            .ReturnsAsync("fake-token");

        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _sut.SubmitProcessAsync(
            "client-123",
            "order",
            new { orderId = "ORD-001" });

        // Assert
        result.ProcessId.Should().BeNull();
        result.Status.Should().Be("queued_offline");

        _offlineQueueMock.Verify(
            x => x.EnqueueAsync(It.IsAny<SubmitProcessRequest>(), default),
            Times.Once);
    }
}
```

---

## Development Roadmap

### Phase 1: Core SDK (Week 1-2)

**Sprint 1.1: Foundation**
- [ ] Create StarGate.Client project
- [ ] Define interfaces (IStarGateClient, ITokenProvider, IOfflineQueue)
- [ ] Implement StarGateClient base class
- [ ] Configure HttpClient and DI extensions

**Sprint 1.2: Authentication**
- [ ] Implement OAuth2TokenProvider
- [ ] Add token caching logic
- [ ] Handle token refresh
- [ ] Unit tests for authentication

### Phase 2: Polling & Offline Queue (Week 3)

**Sprint 2.1: Polling**
- [ ] Implement ProcessPoller with adaptive strategy
- [ ] Add configurable intervals
- [ ] Implement timeout handling
- [ ] Unit tests for polling logic

**Sprint 2.2: Offline Queue**
- [ ] Implement FileBasedOfflineQueue
- [ ] Add queue persistence
- [ ] Implement flush mechanism
- [ ] Unit tests for queue operations

### Phase 3: Resilience & Testing (Week 4)

**Sprint 3.1: Resilience**
- [ ] Integrate Polly for retry
- [ ] Add circuit breaker
- [ ] Configure timeout policies
- [ ] Unit tests for resilience

**Sprint 3.2: Integration Testing**
- [ ] Create integration test suite
- [ ] Test against mock API
- [ ] Test offline scenarios
- [ ] Performance testing

### Phase 4: Documentation & Release (Week 5)

**Sprint 4.1: Documentation**
- [ ] Complete XML documentation
- [ ] Create usage examples
- [ ] Write integration guide
- [ ] Create troubleshooting guide

**Sprint 4.2: Release Preparation**
- [ ] Code review and refactoring
- [ ] Create NuGet package
- [ ] Version 1.0.0 release
- [ ] Publish to NuGet.org

---

## Open Questions

### Technical Decisions

1. **Token Storage**
   - **Question:** Dove memorizzare i token in ambienti diversi (desktop, mobile, server)?
   - **Options:** In-memory, encrypted file, credential manager OS
   - **Decision:** TBD

2. **Offline Queue Size Limit**
   - **Question:** Dovremmo limitare la dimensione della coda offline?
   - **Options:** Unlimited, size-based (MB), count-based
   - **Decision:** TBD

3. **Retry Strategy per Offline Queue**
   - **Question:** Come gestire i retry delle richieste offline?
   - **Options:** Automatic background, manual flush, scheduled
   - **Decision:** TBD

4. **Logging Integration**
   - **Question:** Quale livello di logging fornire di default?
   - **Options:** Minimal, verbose with PII redaction
   - **Decision:** TBD

---

**Document Status:** Draft - Future Implementation  
**Next Review:** TBD  
**Owner:** Development Team
