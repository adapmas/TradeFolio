using System.Net;
using System.Text.Json;
using Xunit;

namespace TradeService.IntegrationTests;

[Collection("postgres")]
public class CorrelationIdApiTests
{
    private const string Header = "X-Correlation-Id";
    private readonly PostgresFixture _db;

    public CorrelationIdApiTests(PostgresFixture db) => _db = db;

    [Fact]
    public async Task A_valid_correlation_id_is_echoed_back()
    {
        using var client = _db.Api.CreateClient();
        var id = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation(Header, id);

        using var response = await client.SendAsync(request);

        Assert.Equal(id, response.Headers.GetValues(Header).Single());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc; DROP TABLE trades;--")]
    public async Task A_missing_or_unsafe_correlation_id_is_replaced_with_a_new_guid(string? supplied)
    {
        using var client = _db.Api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        if (supplied is not null)
        {
            request.Headers.TryAddWithoutValidation(Header, supplied);
        }

        using var response = await client.SendAsync(request);

        var returned = response.Headers.GetValues(Header).Single();
        Assert.True(Guid.TryParse(returned, out _));
        Assert.NotEqual(supplied, returned);
    }

    [Fact]
    public async Task Error_responses_use_the_correlation_id_as_their_trace_id()
    {
        using var client = _db.Api.CreateClient();
        var id = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/trades");
        request.Headers.TryAddWithoutValidation(Header, id);

        using var response = await client.SendAsync(request);   // no token: 401

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, doc.RootElement.GetProperty("traceId").GetString());
    }
}