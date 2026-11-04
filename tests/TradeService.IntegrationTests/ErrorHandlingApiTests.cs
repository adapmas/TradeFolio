using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace TradeService.IntegrationTests;

[Collection("postgres")]
public class ErrorHandlingApiTests
{
    private readonly PostgresFixture _db;

    public ErrorHandlingApiTests(PostgresFixture db) => _db = db;

    [Fact]
    public async Task Malformed_json_gets_a_safe_400_in_the_standard_shape()
    {
        using var client = _db.Api.CreateClient();

        using var response = await client.PostAsync(
            "/api/v1/auth/login", new StringContent("{not json", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("StackTrace", body);
        Assert.DoesNotContain("System.", body);

        using var doc = JsonDocument.Parse(body);
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task The_temporary_ping_endpoints_are_gone()
    {
        using var client = _db.Api.CreateClient();

        using var ping = await client.GetAsync("/api/v1/ping");
        using var pingError = await client.GetAsync("/api/v1/ping/error");

        Assert.Equal(HttpStatusCode.NotFound, ping.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, pingError.StatusCode);
    }
}