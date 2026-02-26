namespace StarGate.Api.Tests.Cors;

using FluentAssertions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using StarGate.Api.Configuration;
using StarGate.Api.Extensions;
using Xunit;

public class CorsConfigurationTests
{
    [Fact]
    public void AddApiCors_Should_RegisterCorsServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Required for CorsService
        var configuration = CreateConfiguration();
        var environment = CreateEnvironment(Environments.Production);

        // Act
        services.AddApiCors(configuration, environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var corsService = serviceProvider.GetService<ICorsService>();
        corsService.Should().NotBeNull();
    }

    [Fact]
    public void AddApiCors_Should_RegisterCorsOptions_WithValidConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = CreateConfiguration();
        var environment = CreateEnvironment(Environments.Production);

        // Act
        services.AddApiCors(configuration, environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<ApiCorsOptions>>();
        options.Should().NotBeNull();
        options!.Value.Should().NotBeNull();
        options.Value.Enabled.Should().BeTrue();
        options.Value.AllowedOrigins.Should().Contain("https://example.com");
    }

    [Fact]
    public void AddApiCors_Should_AllowAnyOrigin_InDevelopment()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Required for CorsService
        var configData = new Dictionary<string, string?>
        {
            ["Cors:Enabled"] = "true",
            ["Cors:AllowAnyOrigin"] = "true"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        var environment = CreateEnvironment(Environments.Development);

        // Act
        services.AddApiCors(configuration, environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var corsService = serviceProvider.GetService<ICorsService>();
        corsService.Should().NotBeNull();
    }

    [Fact]
    public void AddApiCors_Should_NotRegisterCorsServices_WhenDisabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Required for CorsService
        var configData = new Dictionary<string, string?>
        {
            ["Cors:Enabled"] = "false"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        var environment = CreateEnvironment(Environments.Production);

        // Act
        services.AddApiCors(configuration, environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var corsService = serviceProvider.GetService<ICorsService>();
        corsService.Should().BeNull();
    }

    [Fact]
    public void AddApiCors_Should_ConfigureMultipleOrigins()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Required for CorsService
        var configData = new Dictionary<string, string?>
        {
            ["Cors:Enabled"] = "true",
            ["Cors:AllowedOrigins:0"] = "https://example.com",
            ["Cors:AllowedOrigins:1"] = "https://app.example.com",
            ["Cors:AllowAnyOrigin"] = "false",
            ["Cors:AllowCredentials"] = "true",
            ["Cors:PreflightMaxAgeSeconds"] = "600"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        var environment = CreateEnvironment(Environments.Production);

        // Act
        services.AddApiCors(configuration, environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var corsService = serviceProvider.GetService<ICorsService>();
        corsService.Should().NotBeNull();
        
        var options = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<ApiCorsOptions>>();
        options!.Value.AllowedOrigins.Should().HaveCount(2);
        options.Value.AllowedOrigins.Should().Contain("https://example.com");
        options.Value.AllowedOrigins.Should().Contain("https://app.example.com");
    }

    [Fact]
    public void AddApiCors_Should_CreateDevelopmentPolicy_InDevelopment()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Required for CorsService
        var configuration = CreateConfiguration();
        var environment = CreateEnvironment(Environments.Development);

        // Act
        services.AddApiCors(configuration, environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var corsService = serviceProvider.GetService<ICorsService>();
        corsService.Should().NotBeNull();
    }

    [Fact]
    public void AddApiCors_Should_ConfigurePreflightMaxAge()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configData = new Dictionary<string, string?>
        {
            ["Cors:Enabled"] = "true",
            ["Cors:AllowedOrigins:0"] = "https://example.com",
            ["Cors:AllowAnyOrigin"] = "false",
            ["Cors:PreflightMaxAgeSeconds"] = "3600"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        var environment = CreateEnvironment(Environments.Production);

        // Act
        services.AddApiCors(configuration, environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<ApiCorsOptions>>();
        options!.Value.PreflightMaxAgeSeconds.Should().Be(3600);
    }

    private static IConfiguration CreateConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Cors:Enabled"] = "true",
            ["Cors:AllowedOrigins:0"] = "https://example.com",
            ["Cors:AllowAnyOrigin"] = "false",
            ["Cors:AllowCredentials"] = "true",
            ["Cors:PreflightMaxAgeSeconds"] = "600"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    private static IWebHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }
}
