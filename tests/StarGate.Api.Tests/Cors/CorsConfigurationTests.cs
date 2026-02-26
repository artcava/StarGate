namespace StarGate.Api.Tests.Cors;

using FluentAssertions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    public void AddApiCors_Should_ThrowException_WhenNoOriginsConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            ["Cors:Enabled"] = "true",
            ["Cors:AllowAnyOrigin"] = "false",
            ["Cors:AllowedOrigins:0"] = "" // Empty
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        var environment = CreateEnvironment(Environments.Production);

        // Act
        Action act = () =>
        {
            services.AddApiCors(configuration, environment);
            var serviceProvider = services.BuildServiceProvider();
            var corsService = serviceProvider.GetRequiredService<ICorsService>();
        };

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no allowed origins*");
    }

    [Fact]
    public void AddApiCors_Should_AllowAnyOrigin_InDevelopment()
    {
        // Arrange
        var services = new ServiceCollection();
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
    public void AddApiCors_Should_NotRegisterServices_WhenDisabled()
    {
        // Arrange
        var services = new ServiceCollection();
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
    }

    [Fact]
    public void AddApiCors_Should_CreateDevelopmentPolicy_InDevelopment()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();
        var environment = CreateEnvironment(Environments.Development);

        // Act
        services.AddApiCors(configuration, environment);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var corsService = serviceProvider.GetService<ICorsService>();
        corsService.Should().NotBeNull();
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
