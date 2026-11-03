using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TradeFolio.Common.Correlation;
using TradeFolio.Common.Security;
using Xunit;

namespace Gateway.IntegrationTests;

/// <summary>The real gateway in memory. Nothing listens at the downstream address, so any
/// request that passes the gateway's own checks ends in a proxy error (502/503/504).</summary>
public sealed class GatewayFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "gateway-tests-only-signing-key-0123456789";
    private const string DeadAddress = "http://127.0.0.1:9/";

    public GatewayFactory()
    {
        // Program.cs reads configuration while building, so use environment variables (always seen early).
        Environment.SetEnvironmentVariable("Jwt__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable(
            "ReverseProxy__Clusters__trade__Destinations__tradeservice__Address", DeadAddress);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("ReverseProxy:Clusters:trade:Destinations:tradeservice:Address", DeadAddress);
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
    }
}

public class GatewayTests : IClassFixture<GatewayFactory>
{
    private static readonly HttpStatusCode[] ProxyFailure =
        [HttpStatusCode.BadGateway, HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout];

    private readonly GatewayFactory _factory;

    public GatewayTests(GatewayFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_is_open()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- Authentication at the edge ----------

    [Theory]
    [InlineData("/api/v1/trades")]
    [InlineData("/api/v1/trades/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/v1/holdings")]
    [InlineData("/api/v1/users/me")]
    public async Task Protected_routes_return_401_without_a_token(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Garbage_wrong_key_and_expired_tokens_are_rejected()
    {
        var tokens = new[]
        {
            "this.is.not-a-token",
            Token("some-other-signing-key-0123456789-abcdef", DateTimeOffset.UtcNow),
            Token(GatewayFactory.SigningKey, DateTimeOffset.UtcNow.AddHours(-2))
        };

        foreach (var token in tokens)
        {
            using var client = ClientWith(token);
            using var response = await client.GetAsync("/api/v1/trades");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_valid_token_gets_past_the_gateway()
    {
        using var client = ClientWith(Token(GatewayFactory.SigningKey, DateTimeOffset.UtcNow));

        using var response = await client.GetAsync("/api/v1/trades");

        // Not 401: the gateway accepted the token and tried to proxy (the test downstream is down).
        Assert.Contains(response.StatusCode, ProxyFailure);
    }

    [Theory]
    [InlineData("POST", "/api/v1/auth/login")]
    [InlineData("POST", "/api/v1/auth/register")]
    [InlineData("GET", "/api/v1/instruments")]
    public async Task Public_routes_are_proxied_without_a_token(string method, string path)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request);

        Assert.Contains(response.StatusCode, ProxyFailure);
    }

    // ---------- Versioning (FR-22) ----------

    [Theory]
    [InlineData("/api/v2/trades")]
    [InlineData("/api/trades")]
    [InlineData("/trades")]
    [InlineData("/api/v1/unknown")]
    public async Task Only_api_v1_routes_exist(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Correlation ids ----------

    [Fact]
    public async Task A_valid_correlation_id_is_echoed_back()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, id);

        using var response = await client.SendAsync(request);

        Assert.Equal(id, response.Headers.GetValues(CorrelationId.HeaderName).Single());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc; DROP TABLE trades;--")]
    public async Task A_missing_or_unsafe_correlation_id_is_replaced_with_a_new_guid(string? supplied)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        if (supplied is not null)
        {
            request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, supplied);
        }

        using var response = await client.SendAsync(request);

        var returned = response.Headers.GetValues(CorrelationId.HeaderName).Single();
        Assert.True(Guid.TryParse(returned, out _));
        Assert.NotEqual(supplied, returned);
    }

    [Fact]
    public async Task Gateway_errors_also_carry_the_correlation_id()
    {
        using var client = _factory.CreateClient();
        var id = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/trades");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, id);

        using var response = await client.SendAsync(request);   // 401 from the gateway itself

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(id, response.Headers.GetValues(CorrelationId.HeaderName).Single());
    }

    // ---------- helpers ----------

    private HttpClient ClientWith(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string Token(string signingKey, DateTimeOffset issuedAt)
    {
        var options = new JwtOptions { SigningKey = signingKey };
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = issuedAt.AddMinutes(15).UtcDateTime,
            Subject = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())]),
            SigningCredentials = new SigningCredentials(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}