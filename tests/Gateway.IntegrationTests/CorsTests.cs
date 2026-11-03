using System.Net;
using TradeFolio.Gateway;
using Xunit;

namespace Gateway.IntegrationTests;

public class CorsTests : IClassFixture<GatewayFactory>
{
    private const string AllowedOrigin = "http://localhost:5173";
    private readonly GatewayFactory _factory;

    public CorsTests(GatewayFactory factory) => _factory = factory;

    [Fact]
    public async Task Preflight_from_the_frontend_origin_is_allowed()
    {
        using var client = _factory.CreateClient();
        using var request = Preflight("/api/v1/trades", AllowedOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("POST", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
        Assert.Contains("Idempotency-Key", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://evil.example")]
    [InlineData("http://localhost:5174")]
    public async Task Preflight_from_any_other_origin_gets_no_permission(string origin)
    {
        using var client = _factory.CreateClient();
        using var request = Preflight("/api/v1/trades", origin);

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Responses_expose_the_headers_the_frontend_needs()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/trades");
        request.Headers.TryAddWithoutValidation("Origin", AllowedOrigin);

        using var response = await client.SendAsync(request);   // 401, still carries CORS headers

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        var exposed = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers"));
        Assert.Contains("X-Correlation-Id", exposed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retry-After", exposed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_good_origin_list_is_accepted()
    {
        GatewayCors.ValidateOrigins(["http://localhost:5173", "https://app.example.com"]);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://localhost:5173/")]
    [InlineData("localhost:5173")]
    [InlineData("ftp://example.com")]
    [InlineData("https://app.example.com/some/path")]
    [InlineData("https://*.example.com")]
    public void A_wildcard_or_malformed_origin_stops_the_gateway_starting(string origin)
    {
        Assert.Throws<InvalidOperationException>(() => GatewayCors.ValidateOrigins([origin]));
    }

    [Fact]
    public void An_empty_origin_list_stops_the_gateway_starting()
    {
        Assert.Throws<InvalidOperationException>(() => GatewayCors.ValidateOrigins([]));
    }

    private static HttpRequestMessage Preflight(string path, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation(
            "Access-Control-Request-Headers", "authorization,content-type,idempotency-key");
        return request;
    }
}