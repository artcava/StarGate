using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StarGate.Api.Configuration;
using StarGate.Api.Extensions;
using Xunit;

namespace StarGate.Api.Tests.Authentication;

public class JwtAuthenticationTests
{
    [Fact]
    public void AddJwtAuthentication_Should_RegisterAuthenticationServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();
        services.AddLogging();

        // Act
        services.AddJwtAuthentication(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var authenticationSchemeProvider = serviceProvider
            .GetService<IAuthenticationSchemeProvider>();

        authenticationSchemeProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddJwtAuthentication_Should_ConfigureJwtOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();
        services.AddLogging();

        // Act
        services.AddJwtAuthentication(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var jwtOptions = serviceProvider
            .GetRequiredService<IOptions<JwtOptions>>();

        jwtOptions.Value.Should().NotBeNull();
        jwtOptions.Value.Issuer.Should().Be("test-issuer");
        jwtOptions.Value.Audience.Should().Be("test-audience");
        jwtOptions.Value.SecretKey.Should().Be("test-secret-key-at-least-32-characters-long");
    }

    [Fact]
    public void AddJwtAuthentication_Should_ThrowException_WhenConfigurationMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddLogging();

        // Act
        Action act = () => services.AddJwtAuthentication(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT configuration*");
    }

    [Fact]
    public void AddJwtAuthentication_Should_ConfigureDefaultAuthenticationScheme()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();
        services.AddLogging();

        // Act
        services.AddJwtAuthentication(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var authenticationOptions = serviceProvider
            .GetRequiredService<IOptions<AuthenticationOptions>>();

        authenticationOptions.Value.DefaultAuthenticateScheme
            .Should().Be(JwtBearerDefaults.AuthenticationScheme);
        authenticationOptions.Value.DefaultChallengeScheme
            .Should().Be(JwtBearerDefaults.AuthenticationScheme);
    }

    [Fact]
    public void AddJwtAuthentication_Should_ValidateRequiredConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();
        services.AddLogging();

        // Act
        services.AddJwtAuthentication(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var jwtOptions = serviceProvider
            .GetRequiredService<IOptions<JwtOptions>>();

        jwtOptions.Value.Issuer.Should().NotBeNullOrWhiteSpace();
        jwtOptions.Value.Audience.Should().NotBeNullOrWhiteSpace();
        jwtOptions.Value.ValidateLifetime.Should().BeTrue();
        jwtOptions.Value.ClockSkew.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void JwtOptions_Should_HaveCorrectDefaultValues()
    {
        // Arrange & Act
        var options = new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SecretKey = "test-key"
        };

        // Assert
        options.RequireHttpsMetadata.Should().BeTrue();
        options.ValidateLifetime.Should().BeTrue();
        options.ClockSkew.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void JwtOptions_Should_AllowCustomConfiguration()
    {
        // Arrange & Act
        var options = new JwtOptions
        {
            Issuer = "custom-issuer",
            Audience = "custom-audience",
            SecretKey = "custom-secret-key",
            RequireHttpsMetadata = false,
            ValidateLifetime = false,
            ClockSkew = TimeSpan.FromMinutes(10)
        };

        // Assert
        options.Issuer.Should().Be("custom-issuer");
        options.Audience.Should().Be("custom-audience");
        options.SecretKey.Should().Be("custom-secret-key");
        options.RequireHttpsMetadata.Should().BeFalse();
        options.ValidateLifetime.Should().BeFalse();
        options.ClockSkew.Should().Be(TimeSpan.FromMinutes(10));
    }

    private static IConfiguration CreateConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:SecretKey"] = "test-secret-key-at-least-32-characters-long",
            ["Jwt:RequireHttpsMetadata"] = "false",
            ["Jwt:ValidateLifetime"] = "true",
            ["Jwt:ClockSkew"] = "00:05:00"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }
}
