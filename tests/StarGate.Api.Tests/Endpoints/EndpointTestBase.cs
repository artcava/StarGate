using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace StarGate.Api.Tests.Endpoints;

/// <summary>
/// Base class for endpoint tests providing common test utilities.
/// </summary>
public abstract class EndpointTestBase
{
    protected Mock<ILogger> LoggerMock { get; }
    protected DefaultHttpContext HttpContext { get; }
    protected ClaimsPrincipal User { get; set; }

    protected EndpointTestBase()
    {
        LoggerMock = new Mock<ILogger>();
        HttpContext = new DefaultHttpContext();
        User = CreateDefaultUser();
        HttpContext.User = User;
    }

    protected static ClaimsPrincipal CreateDefaultUser(string clientId = "test-client")
    {
        var claims = new List<Claim>
        {
            new Claim("client_id", clientId),
            new Claim(ClaimTypes.NameIdentifier, clientId),
            new Claim("scope", "process.read process.write")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    protected static ClaimsPrincipal CreateAdminUser(string clientId = "admin-client")
    {
        var claims = new List<Claim>
        {
            new Claim("client_id", clientId),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("scope", "process.read process.write admin")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    protected static T GetResultValue<T>(IResult result)
    {
        if (result is Ok<T> okResult)
        {
            return okResult.Value!;
        }

        if (result is Created<T> createdResult)
        {
            return createdResult.Value!;
        }

        if(result is Accepted<T> acceptedResult)
        {
            return acceptedResult.Value!;
        }

        throw new InvalidOperationException($"Result is not Ok<{typeof(T).Name}> or Created<{typeof(T).Name}>");
    }

    protected static int GetStatusCode(IResult result)
    {
        return result switch
        {
            IStatusCodeHttpResult statusCodeResult => statusCodeResult.StatusCode ?? 200,
            _ => 200
        };
    }
}
