using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TradeService.IntegrationTests;

/// <summary>Starts the real TradeService in memory, pointed at the Testcontainers database.</summary>
public sealed class TradeApiFactory : WebApplicationFactory<Program>
{
    // Test-only key (never used anywhere else). Longer than the 32-byte minimum.
    public const string SigningKey = "integration-tests-only-signing-key-0123456789";

    private readonly string _connectionString;

    public TradeApiFactory(string connectionString)
    {
        _connectionString = connectionString;

        // Program.cs reads configuration while it is still building the app, so these must
        // be in place before the host starts. Environment variables are always seen that early.
        Environment.SetEnvironmentVariable("ConnectionStrings__TradeDb", connectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", SigningKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:TradeDb", _connectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
    }
}
