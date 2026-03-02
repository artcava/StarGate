using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StarGate.Core.Domain;
using StarGate.Server.Handlers;
using Xunit;

namespace StarGate.Server.Tests.Handlers;

public class ShippingProcessHandlerTests
{
    // Use deterministic seed for consistent test results
    // Production code uses time-based random (no seed)
    private const int TestRandomSeed = 42;
    private readonly ShippingProcessHandler _handler;

    public ShippingProcessHandlerTests()
    {
        _handler = new ShippingProcessHandler(
            NullLogger<ShippingProcessHandler>.Instance,
            randomSeed: TestRandomSeed);
    }

    [Fact]
    public void ProcessType_Should_ReturnShipping()
    {
        // Act
        var processType = _handler.ProcessType;

        // Assert
        processType.Should().Be("shipping");
    }

    [Fact]
    public async Task ExecuteAsync_Should_ThrowInvalidOperationException_WhenShipmentIdMissing()
    {
        // Arrange
        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ClientId = "test-client",
            ProcessType = "shipping",
            ClientProcessId = "ship-123",
            Metadata = new Dictionary<string, string>
            {
                ["orderId"] = "order-456",
                ["destination"] = "New York, NY",
                ["carrier"] = "UPS"
            }
        };

        // Act
        var act = async () => await _handler.ExecuteAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Shipment ID*");
    }

    [Fact]
    public async Task ExecuteAsync_Should_ThrowInvalidOperationException_WhenOrderIdMissing()
    {
        // Arrange
        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ClientId = "test-client",
            ProcessType = "shipping",
            ClientProcessId = "ship-123",
            Metadata = new Dictionary<string, string>
            {
                ["shipmentId"] = "ship-789",
                ["destination"] = "New York, NY",
                ["carrier"] = "UPS"
            }
        };

        // Act
        var act = async () => await _handler.ExecuteAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Order ID*");
    }

    [Fact]
    public async Task ExecuteAsync_Should_ThrowInvalidOperationException_WhenDestinationMissing()
    {
        // Arrange
        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ClientId = "test-client",
            ProcessType = "shipping",
            ClientProcessId = "ship-123",
            Metadata = new Dictionary<string, string>
            {
                ["shipmentId"] = "ship-789",
                ["orderId"] = "order-456",
                ["carrier"] = "UPS"
            }
        };

        // Act
        var act = async () => await _handler.ExecuteAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Destination*");
    }

    [Fact]
    public async Task ExecuteAsync_Should_ThrowInvalidOperationException_WhenCarrierMissing()
    {
        // Arrange
        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ClientId = "test-client",
            ProcessType = "shipping",
            ClientProcessId = "ship-123",
            Metadata = new Dictionary<string, string>
            {
                ["shipmentId"] = "ship-789",
                ["orderId"] = "order-456",
                ["destination"] = "New York, NY"
            }
        };

        // Act
        var act = async () => await _handler.ExecuteAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Carrier*");
    }

    [Fact]
    public async Task ExecuteAsync_Should_ThrowInvalidOperationException_WhenCarrierInvalid()
    {
        // Arrange
        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ClientId = "test-client",
            ProcessType = "shipping",
            ClientProcessId = "ship-123",
            Metadata = new Dictionary<string, string>
            {
                ["shipmentId"] = "ship-789",
                ["orderId"] = "order-456",
                ["destination"] = "New York, NY",
                ["carrier"] = "INVALID_CARRIER"
            }
        };

        // Act
        var act = async () => await _handler.ExecuteAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid carrier*");
    }

    [Theory]
    [InlineData("UPS")]
    [InlineData("FEDEX")]
    [InlineData("DHL")]
    [InlineData("USPS")]
    public async Task ExecuteAsync_Should_AcceptValidCarriers(string carrier)
    {
        // Arrange
        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ClientId = "test-client",
            ProcessType = "shipping",
            ClientProcessId = "ship-123",
            Metadata = new Dictionary<string, string>
            {
                ["shipmentId"] = "ship-789",
                ["orderId"] = "order-456",
                ["destination"] = "New York, NY",
                ["carrier"] = carrier
            }
        };

        // Act & Assert - Deterministic with seed=42
        await _handler.ExecuteAsync(context);
    }

    [Theory]
    [InlineData("ups")]
    [InlineData("fedex")]
    [InlineData("dhl")]
    [InlineData("usps")]
    public async Task ExecuteAsync_Should_AcceptCarriersInLowercase(string carrier)
    {
        // Arrange
        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ClientId = "test-client",
            ProcessType = "shipping",
            ClientProcessId = "ship-123",
            Metadata = new Dictionary<string, string>
            {
                ["shipmentId"] = "ship-789",
                ["orderId"] = "order-456",
                ["destination"] = "New York, NY",
                ["carrier"] = carrier
            }
        };

        // Act & Assert
        await _handler.ExecuteAsync(context);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ThrowOperationCanceledException_WhenCancellationRequested()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ClientId = "test-client",
            ProcessType = "shipping",
            ClientProcessId = "ship-123",
            Metadata = new Dictionary<string, string>
            {
                ["shipmentId"] = "ship-789",
                ["orderId"] = "order-456",
                ["destination"] = "New York, NY",
                ["carrier"] = "UPS"
            },
            CancellationToken = cts.Token
        };

        // Act
        var act = async () => await _handler.ExecuteAsync(context);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_Should_CompleteSuccessfully_WithValidData()
    {
        // Arrange
        var context = new ProcessContext
        {
            ProcessId = Guid.NewGuid(),
            ClientId = "test-client",
            ProcessType = "shipping",
            ClientProcessId = "ship-123",
            Metadata = new Dictionary<string, string>
            {
                ["shipmentId"] = "ship-789",
                ["orderId"] = "order-456",
                ["destination"] = "New York, NY",
                ["carrier"] = "UPS"
            }
        };

        // Act & Assert - Deterministic with seed=42
        await _handler.ExecuteAsync(context);
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act
        var act = () => new ShippingProcessHandler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_Should_AcceptRandomSeed()
    {
        // Act
        var handler = new ShippingProcessHandler(
            NullLogger<ShippingProcessHandler>.Instance,
            randomSeed: 123);

        // Assert
        handler.Should().NotBeNull();
        handler.ProcessType.Should().Be("shipping");
    }

    [Fact]
    public void Constructor_Should_UseTimeBasedRandom_WhenSeedNotProvided()
    {
        // Act
        var handler = new ShippingProcessHandler(
            NullLogger<ShippingProcessHandler>.Instance);

        // Assert
        handler.Should().NotBeNull();
        handler.ProcessType.Should().Be("shipping");
    }
}
