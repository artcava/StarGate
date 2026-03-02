using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Core.Abstractions;
using StarGate.Server.Factories;

namespace StarGate.Server.Tests.Factories;

public class ProcessHandlerFactoryTests
{
    private readonly ProcessHandlerFactory _factory;
    private readonly Mock<IProcessHandler> _handlerMock;

    public ProcessHandlerFactoryTests()
    {
        _factory = new ProcessHandlerFactory(NullLogger<ProcessHandlerFactory>.Instance);
        _handlerMock = new Mock<IProcessHandler>();
        _handlerMock.Setup(h => h.ProcessType).Returns("test-process");
    }

    [Fact]
    public void Constructor_Should_CreateEmptyFactory()
    {
        // Arrange & Act
        var factory = new ProcessHandlerFactory(NullLogger<ProcessHandlerFactory>.Instance);

        // Assert
        factory.GetRegisteredProcessTypes().Should().BeEmpty();
    }

    [Fact]
    public void RegisterHandler_Should_AddHandler_WhenValid()
    {
        // Act
        _factory.RegisterHandler("test-process", _handlerMock.Object);

        // Assert
        _factory.IsRegistered("test-process").Should().BeTrue();
        _factory.GetHandler("test-process").Should().Be(_handlerMock.Object);
    }

    [Fact]
    public void RegisterHandler_Should_ThrowArgumentException_WhenProcessTypeIsNull()
    {
        // Act
        var act = () => _factory.RegisterHandler(null!, _handlerMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("processType");
    }

    [Fact]
    public void RegisterHandler_Should_ThrowArgumentException_WhenProcessTypeIsEmpty()
    {
        // Act
        var act = () => _factory.RegisterHandler(string.Empty, _handlerMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("processType");
    }

    [Fact]
    public void RegisterHandler_Should_ThrowArgumentException_WhenProcessTypeIsWhitespace()
    {
        // Act
        var act = () => _factory.RegisterHandler("   ", _handlerMock.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("processType");
    }

    [Fact]
    public void RegisterHandler_Should_ThrowArgumentNullException_WhenHandlerIsNull()
    {
        // Act
        var act = () => _factory.RegisterHandler("test", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("handler");
    }

    [Fact]
    public void RegisterHandler_Should_ThrowInvalidOperationException_WhenProcessTypeMismatch()
    {
        // Arrange
        _handlerMock.Setup(h => h.ProcessType).Returns("different-type");

        // Act
        var act = () => _factory.RegisterHandler("test-process", _handlerMock.Object);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not match registration key*");
    }

    [Fact]
    public void RegisterHandler_Should_ThrowInvalidOperationException_WhenHandlerAlreadyRegistered()
    {
        // Arrange
        _factory.RegisterHandler("test-process", _handlerMock.Object);

        var secondHandler = new Mock<IProcessHandler>();
        secondHandler.Setup(h => h.ProcessType).Returns("test-process");

        // Act
        var act = () => _factory.RegisterHandler("test-process", secondHandler.Object);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void GetHandler_Should_ReturnNull_WhenNotRegistered()
    {
        // Act
        var result = _factory.GetHandler("unknown-type");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetHandler_Should_ReturnNull_WhenProcessTypeIsNull()
    {
        // Act
        var result = _factory.GetHandler(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetHandler_Should_ReturnNull_WhenProcessTypeIsEmpty()
    {
        // Act
        var result = _factory.GetHandler(string.Empty);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetHandler_Should_ReturnNull_WhenProcessTypeIsWhitespace()
    {
        // Act
        var result = _factory.GetHandler("   ");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetHandler_Should_BeCaseInsensitive()
    {
        // Arrange
        _factory.RegisterHandler("test-process", _handlerMock.Object);

        // Act
        var result1 = _factory.GetHandler("TEST-PROCESS");
        var result2 = _factory.GetHandler("Test-Process");
        var result3 = _factory.GetHandler("test-process");

        // Assert
        result1.Should().Be(_handlerMock.Object);
        result2.Should().Be(_handlerMock.Object);
        result3.Should().Be(_handlerMock.Object);
    }

    [Fact]
    public void GetRegisteredProcessTypes_Should_ReturnEmptyCollection_WhenNoHandlersRegistered()
    {
        // Act
        var types = _factory.GetRegisteredProcessTypes().ToList();

        // Assert
        types.Should().BeEmpty();
    }

    [Fact]
    public void GetRegisteredProcessTypes_Should_ReturnAllTypes()
    {
        // Arrange
        var handler1 = new Mock<IProcessHandler>();
        handler1.Setup(h => h.ProcessType).Returns("type1");

        var handler2 = new Mock<IProcessHandler>();
        handler2.Setup(h => h.ProcessType).Returns("type2");

        _factory.RegisterHandler("type1", handler1.Object);
        _factory.RegisterHandler("type2", handler2.Object);

        // Act
        var types = _factory.GetRegisteredProcessTypes().ToList();

        // Assert
        types.Should().HaveCount(2);
        types.Should().Contain("type1");
        types.Should().Contain("type2");
    }

    [Fact]
    public void IsRegistered_Should_ReturnTrue_WhenHandlerExists()
    {
        // Arrange
        _factory.RegisterHandler("test-process", _handlerMock.Object);

        // Act
        var result = _factory.IsRegistered("test-process");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRegistered_Should_ReturnFalse_WhenHandlerDoesNotExist()
    {
        // Act
        var result = _factory.IsRegistered("unknown-type");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRegistered_Should_ReturnFalse_WhenProcessTypeIsNull()
    {
        // Act
        var result = _factory.IsRegistered(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRegistered_Should_ReturnFalse_WhenProcessTypeIsEmpty()
    {
        // Act
        var result = _factory.IsRegistered(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRegistered_Should_ReturnFalse_WhenProcessTypeIsWhitespace()
    {
        // Act
        var result = _factory.IsRegistered("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRegistered_Should_BeCaseInsensitive()
    {
        // Arrange
        _factory.RegisterHandler("test-process", _handlerMock.Object);

        // Act
        var result1 = _factory.IsRegistered("TEST-PROCESS");
        var result2 = _factory.IsRegistered("Test-Process");
        var result3 = _factory.IsRegistered("test-process");

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();
    }

    [Fact]
    public void Factory_Should_BeThreadSafe_WhenRegisteringMultipleHandlers()
    {
        // Arrange
        var handlers = Enumerable.Range(0, 100)
            .Select(i =>
            {
                var mock = new Mock<IProcessHandler>();
                mock.Setup(h => h.ProcessType).Returns($"type-{i}");
                return (Type: $"type-{i}", Handler: mock.Object);
            })
            .ToList();

        // Act
        Parallel.ForEach(handlers, item =>
        {
            _factory.RegisterHandler(item.Type, item.Handler);
        });

        // Assert
        _factory.GetRegisteredProcessTypes().Should().HaveCount(100);
        foreach (var item in handlers)
        {
            _factory.IsRegistered(item.Type).Should().BeTrue();
            _factory.GetHandler(item.Type).Should().Be(item.Handler);
        }
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act
        var act = () => new ProcessHandlerFactory(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
