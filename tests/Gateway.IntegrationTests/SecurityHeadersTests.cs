using System.Net;
using Xunit;

namespace Gateway.IntegrationTests;

public class SecurityHeadersTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public SecurityHeadersTests(GatewayFactory factory) => _factory = factory;

    [Fact]
    public async Task Responses_carry_the_hardening_headers()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task Error_responses_carry_them_too()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/trades");   // 401 from the gateway

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
    }
}