using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;

namespace StarGate.Server.Workers;

/// <summary>
/// Background service that periodically scans for timed-out processes.
/// Runs every 1 minute to identify active processes that have exceeded their timeout.
/// Processes up to 100 timed-out processes per scan to prevent memory issues.
/// </summary>
public class TimeoutScannerWorker : BackgroundService
{
    private readonly IProcessRepository _processRepository;
    private readonly IProcessService _processService;
    private readonly ILogger<TimeoutScannerWorker> _logger;
    private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(1);

    public TimeoutScannerWorker(
        IProcessRepository processRepository,
        IProcessService processService,
        ILogger<TimeoutScannerWorker> logger)
    {
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TimeoutScannerWorker starting. Scan interval: {ScanInterval}s",
            _scanInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanForTimedOutProcessesAsync(stoppingToken);
                await Task.Delay(_scanInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("TimeoutScannerWorker stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error during timeout scan. Will retry in {ScanInterval}s",
                    _scanInterval.TotalSeconds);

                await Task.Delay(_scanInterval, stoppingToken);
            }
        }

        _logger.LogInformation("TimeoutScannerWorker stopped");
    }

    private async Task ScanForTimedOutProcessesAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Scanning for timed-out processes");

        // Get active processes that have timed out
        var timedOutProcesses = await _processRepository.GetTimedOutProcessesAsync(
            cancellationToken);

        if (!timedOutProcesses.Any())
        {
            _logger.LogDebug("No timed-out processes found");
            return;
        }

        _logger.LogInformation(
            "Found {Count} timed-out process(es)",
            timedOutProcesses.Count);

        var failedCount = 0;
        var successCount = 0;

        foreach (var process in timedOutProcesses)
        {
            try
            {
                _logger.LogWarning(
                    "Failing timed-out process: ProcessId={ProcessId}, TimeoutAt={TimeoutAt}, Status={Status}",
                    process.ProcessId,
                    process.TimeoutAt,
                    process.Status);

                await _processService.CheckTimeoutAsync(
                    process.ProcessId,
                    cancellationToken);

                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to handle timed-out process: ProcessId={ProcessId}",
                    process.ProcessId);

                failedCount++;
            }
        }

        _logger.LogInformation(
            "Timeout scan completed: Success={Success}, Failed={Failed}",
            successCount,
            failedCount);
    }
}
