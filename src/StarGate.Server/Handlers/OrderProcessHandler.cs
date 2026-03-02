using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;

namespace StarGate.Server.Handlers;

/// <summary>
/// Process handler for order processing operations.
/// </summary>
public class OrderProcessHandler : IProcessHandler
{
    private readonly ILogger<OrderProcessHandler> _logger;

    public OrderProcessHandler(ILogger<OrderProcessHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProcessType => "order";

    public async Task ExecuteAsync(ProcessContext context)
    {
        _logger.LogInformation(
            "Starting order processing: ProcessId={ProcessId}, ClientId={ClientId}, ClientProcessId={ClientProcessId}",
            context.ProcessId,
            context.ClientId,
            context.ClientProcessId);

        try
        {
            // Extract order metadata
            var orderId = context.GetMetadata("orderId");
            var customerId = context.GetMetadata("customerId");
            var amount = context.GetMetadata("amount");

            ValidateOrderData(orderId, customerId, amount);

            _logger.LogDebug(
                "Order validated: OrderId={OrderId}, CustomerId={CustomerId}, Amount={Amount}",
                orderId,
                customerId,
                amount);

            // Step 1: Validate inventory
            await ValidateInventoryAsync(orderId!, context.CancellationToken);
            _logger.LogInformation("Inventory validated: OrderId={OrderId}", orderId);

            // Step 2: Process payment
            await ProcessPaymentAsync(customerId!, amount!, context.CancellationToken);
            _logger.LogInformation("Payment processed: OrderId={OrderId}, Amount={Amount}", orderId, amount);

            // Step 3: Update order status
            await UpdateOrderStatusAsync(orderId!, "Confirmed", context.CancellationToken);
            _logger.LogInformation("Order confirmed: OrderId={OrderId}", orderId);

            // Step 4: Trigger fulfillment
            await TriggerFulfillmentAsync(orderId!, context.CancellationToken);
            _logger.LogInformation("Fulfillment triggered: OrderId={OrderId}", orderId);

            _logger.LogInformation(
                "Order processing completed successfully: ProcessId={ProcessId}, OrderId={OrderId}",
                context.ProcessId,
                orderId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Order processing cancelled: ProcessId={ProcessId}",
                context.ProcessId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                ex,
                "Order validation failed: ProcessId={ProcessId}",
                context.ProcessId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Order processing failed: ProcessId={ProcessId}",
                context.ProcessId);
            throw;
        }
    }

    private static void ValidateOrderData(string? orderId, string? customerId, string? amount)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new InvalidOperationException("Order ID is required");
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new InvalidOperationException("Customer ID is required");
        }

        if (string.IsNullOrWhiteSpace(amount) || !decimal.TryParse(amount, out var parsedAmount) || parsedAmount <= 0)
        {
            throw new InvalidOperationException("Valid amount is required");
        }
    }

    private async Task ValidateInventoryAsync(string orderId, CancellationToken cancellationToken)
    {
        // Simulate external API call to inventory service
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

        _logger.LogDebug(
            "Inventory service called: OrderId={OrderId}",
            orderId);

        // Simulate inventory check (could fail with probability)
        var random = new Random();
        if (random.Next(100) < 5) // 5% failure rate
        {
            throw new InvalidOperationException($"Insufficient inventory for order {orderId}");
        }
    }

    private async Task ProcessPaymentAsync(string customerId, string amount, CancellationToken cancellationToken)
    {
        // Simulate external API call to payment gateway
        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);

        _logger.LogDebug(
            "Payment gateway called: CustomerId={CustomerId}, Amount={Amount}",
            customerId,
            amount);

        // Simulate payment processing (could fail with probability)
        var random = new Random();
        if (random.Next(100) < 3) // 3% failure rate
        {
            throw new HttpRequestException($"Payment gateway error for customer {customerId}");
        }
    }

    private async Task UpdateOrderStatusAsync(string orderId, string status, CancellationToken cancellationToken)
    {
        // Simulate database update
        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);

        _logger.LogDebug(
            "Order status updated: OrderId={OrderId}, Status={Status}",
            orderId,
            status);
    }

    private async Task TriggerFulfillmentAsync(string orderId, CancellationToken cancellationToken)
    {
        // Simulate message to fulfillment system
        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);

        _logger.LogDebug(
            "Fulfillment message sent: OrderId={OrderId}",
            orderId);
    }
}
