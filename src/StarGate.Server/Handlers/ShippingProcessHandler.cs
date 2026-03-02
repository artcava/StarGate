using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;

namespace StarGate.Server.Handlers;

/// <summary>
/// Process handler for shipping and logistics operations.
/// </summary>
public class ShippingProcessHandler : IProcessHandler
{
    private readonly ILogger<ShippingProcessHandler> _logger;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShippingProcessHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="randomSeed">Optional seed for Random. Use for deterministic testing. Default: null (time-based).</param>
    public ShippingProcessHandler(
        ILogger<ShippingProcessHandler> logger,
        int? randomSeed = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
    }

    public string ProcessType => "shipping";

    public async Task ExecuteAsync(ProcessContext context)
    {
        _logger.LogInformation(
            "Starting shipping processing: ProcessId={ProcessId}, ClientId={ClientId}, ClientProcessId={ClientProcessId}",
            context.ProcessId,
            context.ClientId,
            context.ClientProcessId);

        try
        {
            // Extract shipping metadata
            var shipmentId = context.GetMetadata("shipmentId");
            var orderId = context.GetMetadata("orderId");
            var destination = context.GetMetadata("destination");
            var carrier = context.GetMetadata("carrier");

            ValidateShippingData(shipmentId, orderId, destination, carrier);

            _logger.LogDebug(
                "Shipping validated: ShipmentId={ShipmentId}, OrderId={OrderId}, Destination={Destination}, Carrier={Carrier}",
                shipmentId,
                orderId,
                destination,
                carrier);

            // Step 1: Calculate shipping cost
            var cost = await CalculateShippingCostAsync(destination!, carrier!, context.CancellationToken);
            _logger.LogInformation(
                "Shipping cost calculated: ShipmentId={ShipmentId}, Cost={Cost}",
                shipmentId,
                cost);

            // Step 2: Reserve carrier capacity
            await ReserveCarrierCapacityAsync(carrier!, shipmentId!, context.CancellationToken);
            _logger.LogInformation(
                "Carrier capacity reserved: ShipmentId={ShipmentId}, Carrier={Carrier}",
                shipmentId,
                carrier);

            // Step 3: Generate shipping label
            var trackingNumber = await GenerateShippingLabelAsync(shipmentId!, destination!, context.CancellationToken);
            _logger.LogInformation(
                "Shipping label generated: ShipmentId={ShipmentId}, TrackingNumber={TrackingNumber}",
                shipmentId,
                trackingNumber);

            // Step 4: Notify warehouse
            await NotifyWarehouseAsync(shipmentId!, orderId!, trackingNumber, context.CancellationToken);
            _logger.LogInformation(
                "Warehouse notified: ShipmentId={ShipmentId}",
                shipmentId);

            // Step 5: Update shipment status
            await UpdateShipmentStatusAsync(shipmentId!, "ReadyToShip", context.CancellationToken);
            _logger.LogInformation(
                "Shipment status updated: ShipmentId={ShipmentId}, Status=ReadyToShip",
                shipmentId);

            _logger.LogInformation(
                "Shipping processing completed successfully: ProcessId={ProcessId}, ShipmentId={ShipmentId}, TrackingNumber={TrackingNumber}",
                context.ProcessId,
                shipmentId,
                trackingNumber);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Shipping processing cancelled: ProcessId={ProcessId}",
                context.ProcessId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                ex,
                "Shipping validation failed: ProcessId={ProcessId}",
                context.ProcessId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Shipping processing failed: ProcessId={ProcessId}",
                context.ProcessId);
            throw;
        }
    }

    private static void ValidateShippingData(
        string? shipmentId,
        string? orderId,
        string? destination,
        string? carrier)
    {
        if (string.IsNullOrWhiteSpace(shipmentId))
        {
            throw new InvalidOperationException("Shipment ID is required");
        }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new InvalidOperationException("Order ID is required");
        }

        if (string.IsNullOrWhiteSpace(destination))
        {
            throw new InvalidOperationException("Destination is required");
        }

        if (string.IsNullOrWhiteSpace(carrier))
        {
            throw new InvalidOperationException("Carrier is required");
        }

        // Validate carrier code
        var validCarriers = new[] { "UPS", "FEDEX", "DHL", "USPS" };
        if (!validCarriers.Contains(carrier.ToUpperInvariant()))
        {
            throw new InvalidOperationException(
                $"Invalid carrier '{carrier}'. Valid carriers: {string.Join(", ", validCarriers)}");
        }
    }

    private async Task<decimal> CalculateShippingCostAsync(
        string destination,
        string carrier,
        CancellationToken cancellationToken)
    {
        // Simulate external API call to shipping cost calculator
        await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);

        _logger.LogDebug(
            "Shipping cost API called: Destination={Destination}, Carrier={Carrier}",
            destination,
            carrier);

        // Simulate cost calculation based on carrier
        var baseCost = carrier.ToUpperInvariant() switch
        {
            "UPS" => 15.99m,
            "FEDEX" => 17.99m,
            "DHL" => 22.99m,
            "USPS" => 12.99m,
            _ => 15.00m
        };

        // Add random variation
        var variation = (decimal)(_random.NextDouble() * 5.0);

        return baseCost + variation;
    }

    private async Task ReserveCarrierCapacityAsync(
        string carrier,
        string shipmentId,
        CancellationToken cancellationToken)
    {
        // Simulate external API call to carrier system
        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);

        _logger.LogDebug(
            "Carrier capacity API called: Carrier={Carrier}, ShipmentId={ShipmentId}",
            carrier,
            shipmentId);

        // Simulate capacity check (could fail with probability)
        if (_random.Next(100) < 2) // 2% failure rate
        {
            throw new HttpRequestException($"Carrier {carrier} has no available capacity");
        }
    }

    private async Task<string> GenerateShippingLabelAsync(
        string shipmentId,
        string destination,
        CancellationToken cancellationToken)
    {
        // Simulate label generation service
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

        _logger.LogDebug(
            "Label generation service called: ShipmentId={ShipmentId}, Destination={Destination}",
            shipmentId,
            destination);

        // Generate tracking number
        var trackingNumber = $"TRK{DateTime.UtcNow:yyyyMMddHHmmss}{shipmentId[^4..]}";

        return trackingNumber;
    }

    private async Task NotifyWarehouseAsync(
        string shipmentId,
        string orderId,
        string trackingNumber,
        CancellationToken cancellationToken)
    {
        // Simulate message to warehouse management system
        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);

        _logger.LogDebug(
            "Warehouse notification sent: ShipmentId={ShipmentId}, OrderId={OrderId}, TrackingNumber={TrackingNumber}",
            shipmentId,
            orderId,
            trackingNumber);
    }

    private async Task UpdateShipmentStatusAsync(
        string shipmentId,
        string status,
        CancellationToken cancellationToken)
    {
        // Simulate database update
        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);

        _logger.LogDebug(
            "Shipment status updated: ShipmentId={ShipmentId}, Status={Status}",
            shipmentId,
            status);
    }
}
