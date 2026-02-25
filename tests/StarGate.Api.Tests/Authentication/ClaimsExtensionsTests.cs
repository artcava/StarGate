using FluentAssertions;
using StarGate.Api.Extensions;
using System.Security.Claims;
using Xunit;

namespace StarGate.Api.Tests.Authentication;

public class ClaimsExtensionsTests
{
    [Fact]
    public void GetClientId_Should_ReturnClientId_WhenClientIdClaimExists()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim("client_id", "test-client"));

        // Act
        var result = principal.GetClientId();

        // Assert
        result.Should().Be("test-client");
    }

    [Fact]
    public void GetClientId_Should_ReturnAzp_WhenClientIdClaimNotExists()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim("azp", "azure-client"));

        // Act
        var result = principal.GetClientId();

        // Assert
        result.Should().Be("azure-client");
    }

    [Fact]
    public void GetClientId_Should_ReturnNameIdentifier_WhenOtherClaimsNotExist()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "user-id"));

        // Act
        var result = principal.GetClientId();

        // Assert
        result.Should().Be("user-id");
    }

    [Fact]
    public void GetClientId_Should_ReturnNull_WhenNoRelevantClaimsExist()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim("other", "value"));

        // Act
        var result = principal.GetClientId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetEmail_Should_ReturnEmail_WhenEmailClaimExists()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimTypes.Email, "test@example.com"));

        // Act
        var result = principal.GetEmail();

        // Assert
        result.Should().Be("test@example.com");
    }

    [Fact]
    public void GetUserName_Should_ReturnUserName_WhenNameClaimExists()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimTypes.Name, "John Doe"));

        // Act
        var result = principal.GetUserName();

        // Assert
        result.Should().Be("John Doe");
    }

    [Fact]
    public void GetRoles_Should_ReturnAllRoles()
    {
        // Arrange
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "User"),
            new Claim("role", "Manager"));

        // Act
        var result = principal.GetRoles().ToList();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("Admin");
        result.Should().Contain("User");
        result.Should().Contain("Manager");
    }

    [Fact]
    public void GetRoles_Should_ReturnDistinctRoles()
    {
        // Arrange
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("role", "Admin"));

        // Act
        var result = principal.GetRoles().ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("Admin");
    }

    [Fact]
    public void HasRole_Should_ReturnTrue_WhenRoleExists()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimTypes.Role, "Admin"));

        // Act
        var result = principal.HasRole("Admin");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasRole_Should_ReturnTrue_WhenRoleExistsWithDifferentCase()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimTypes.Role, "Admin"));

        // Act
        var result = principal.HasRole("admin");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasRole_Should_ReturnFalse_WhenRoleDoesNotExist()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(ClaimTypes.Role, "User"));

        // Act
        var result = principal.HasRole("Admin");

        // Assert
        result.Should().BeFalse();
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuthentication");
        return new ClaimsPrincipal(identity);
    }
}
