using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StarGate.Api.Authorization;
using StarGate.Api.Extensions;
using Xunit;

namespace StarGate.Api.Tests.Authorization;

public class AuthorizationPolicyTests
{
    [Fact]
    public void AddAuthorizationPolicies_Should_RegisterAllPolicies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAuthorizationPolicies();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var authorizationOptions = serviceProvider
            .GetRequiredService<IOptions<AuthorizationOptions>>();

        var policyNames = new[]
        {
            Policies.CreateProcess,
            Policies.ReadOwnProcesses,
            Policies.ReadAllProcesses,
            Policies.AdminOnly
        };

        foreach (var policyName in policyNames)
        {
            var policy = authorizationOptions.Value.GetPolicy(policyName);
            policy.Should().NotBeNull($"policy '{policyName}' should be registered");
        }
    }

    [Fact]
    public void AddAuthorizationPolicies_Should_RegisterAuthorizationHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddAuthorizationPolicies();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var handlers = serviceProvider.GetServices<IAuthorizationHandler>();
        handlers.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateProcess_Policy_Should_RequireAuthentication()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationPolicies();
        var serviceProvider = services.BuildServiceProvider();
        var authorizationOptions = serviceProvider
            .GetRequiredService<IOptions<AuthorizationOptions>>();

        // Act
        var policy = authorizationOptions.Value.GetPolicy(Policies.CreateProcess);

        // Assert
        policy.Should().NotBeNull();
        policy!.Requirements.Should().NotBeEmpty();
    }

    [Fact]
    public void ReadAllProcesses_Policy_Should_RequireAdminRole()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationPolicies();
        var serviceProvider = services.BuildServiceProvider();
        var authorizationOptions = serviceProvider
            .GetRequiredService<IOptions<AuthorizationOptions>>();

        // Act
        var policy = authorizationOptions.Value.GetPolicy(Policies.ReadAllProcesses);

        // Assert
        policy.Should().NotBeNull();
        policy!.Requirements.Should().Contain(r => r is RolesAuthorizationRequirement);
    }

    [Fact]
    public void AdminOnly_Policy_Should_RequireAdminRole()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationPolicies();
        var serviceProvider = services.BuildServiceProvider();
        var authorizationOptions = serviceProvider
            .GetRequiredService<IOptions<AuthorizationOptions>>();

        // Act
        var policy = authorizationOptions.Value.GetPolicy(Policies.AdminOnly);

        // Assert
        policy.Should().NotBeNull();
        policy!.Requirements.Should().Contain(r => r is RolesAuthorizationRequirement);
    }
}
