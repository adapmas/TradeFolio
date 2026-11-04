using System.Text.Json;
using TradeFolio.Contracts;
using TradeFolio.Contracts.Events;
using Xunit;

namespace TradeService.UnitTests;

public class TradeCompletedContractTests
{
    private static TradeCompleted Sample() => new()
    {
        EventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        TradeId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        InstrumentId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Symbol = "AAPL",
        Side = TradeSides.Sell,
        Quantity = 12m,
        Price = 150.00m,
        ExecutedAt = new DateTimeOffset(2026, 10, 16, 18, 30, 0, TimeSpan.Zero),
        CorrelationId = Guid.Parse("55555555-5555-5555-5555-555555555555")
    };

    [Fact]
    public void The_json_has_exactly_the_fields_of_the_spec_in_the_spec_order()
    {
        var json = ContractJson.Serialize(Sample());

        using var doc = JsonDocument.Parse(json);
        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(
            new[]
            {
                "eventId", "eventType", "schemaVersion", "tradeId", "userId", "instrumentId",
                "symbol", "side", "quantity", "price", "executedAt", "correlationId"
            },
            names);
    }

    [Fact]
    public void The_json_values_match_the_spec_example()
    {
        var sample = Sample();

        using var doc = JsonDocument.Parse(ContractJson.Serialize(sample));
        var root = doc.RootElement;

        Assert.Equal(sample.EventId.ToString(), root.GetProperty("eventId").GetString());
        Assert.Equal("TradeCompleted", root.GetProperty("eventType").GetString());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(sample.TradeId.ToString(), root.GetProperty("tradeId").GetString());
        Assert.Equal(sample.UserId.ToString(), root.GetProperty("userId").GetString());
        Assert.Equal(sample.InstrumentId.ToString(), root.GetProperty("instrumentId").GetString());
        Assert.Equal("AAPL", root.GetProperty("symbol").GetString());
        Assert.Equal("SELL", root.GetProperty("side").GetString());
        Assert.Equal(12m, root.GetProperty("quantity").GetDecimal());
        Assert.Equal(150.00m, root.GetProperty("price").GetDecimal());
        Assert.Equal("2026-10-16T18:30:00Z", root.GetProperty("executedAt").GetString());
        Assert.Equal(sample.CorrelationId.ToString(), root.GetProperty("correlationId").GetString());
    }

    [Fact]
    public void A_message_written_in_the_spec_shape_can_be_read()
    {
        const string json = """
            {
              "eventId": "11111111-1111-1111-1111-111111111111",
              "eventType": "TradeCompleted",
              "schemaVersion": 1,
              "tradeId": "22222222-2222-2222-2222-222222222222",
              "userId": "33333333-3333-3333-3333-333333333333",
              "instrumentId": "44444444-4444-4444-4444-444444444444",
              "symbol": "AAPL",
              "side": "SELL",
              "quantity": 12,
              "price": 150.00,
              "executedAt": "2026-10-16T18:30:00Z",
              "correlationId": "55555555-5555-5555-5555-555555555555"
            }
            """;

        var evt = ContractJson.Deserialize<TradeCompleted>(json);

        Assert.Equal(Sample(), evt);
    }

    [Fact]
    public void Writing_then_reading_loses_nothing_including_small_decimals()
    {
        var original = Sample() with { Quantity = 0.000001m, Price = 123456.789012m };

        var copy = ContractJson.Deserialize<TradeCompleted>(ContractJson.Serialize(original));

        Assert.Equal(original, copy);
    }

    [Fact]
    public void Timestamps_are_always_written_in_utc_with_a_z()
    {
        // 12:00 at +05:30 is 06:30 UTC.
        var evt = Sample() with { ExecutedAt = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.FromHours(5.5)) };

        using var doc = JsonDocument.Parse(ContractJson.Serialize(evt));

        Assert.Equal("2026-10-16T06:30:00Z", doc.RootElement.GetProperty("executedAt").GetString());
    }

    [Fact]
    public void Event_type_and_schema_version_are_fixed_by_the_type()
    {
        var evt = Sample();

        Assert.Equal("TradeCompleted", evt.EventType);
        Assert.Equal(1, evt.SchemaVersion);
    }

    [Fact]
    public void A_message_missing_a_required_field_is_rejected()
    {
        // tradeId is missing.
        const string json = """
            {
              "eventId": "11111111-1111-1111-1111-111111111111",
              "eventType": "TradeCompleted",
              "schemaVersion": 1,
              "userId": "33333333-3333-3333-3333-333333333333",
              "instrumentId": "44444444-4444-4444-4444-444444444444",
              "symbol": "AAPL",
              "side": "SELL",
              "quantity": 12,
              "price": 150.00,
              "executedAt": "2026-10-16T18:30:00Z",
              "correlationId": "55555555-5555-5555-5555-555555555555"
            }
            """;

        Assert.Throws<JsonException>(() => ContractJson.Deserialize<TradeCompleted>(json));
    }
}
