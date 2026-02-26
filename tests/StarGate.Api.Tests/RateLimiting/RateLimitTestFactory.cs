using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace StarGate.Api.Tests.RateLimiting;

/// <summary>
/// Custom WebApplicationFactory for rate limiting tests.
/// </summary>
public class RateLimitTestFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _configuration;

    public RateLimitTestFactory(Dictionary<string, string?> configuration)
    {
        _configuration = configuration;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear existing configuration sources and add our test configuration
            config.Sources.Clear();
            config.AddInMemoryCollection(_configuration);
        });
    }
}
