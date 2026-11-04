using TradeFolio.Contracts;
using TradeFolio.Contracts.Events;
using TradeService.Infrastructure.Persistence;
using Xunit;

namespace TradeService.UnitTests;

public class OutboxMessageTests
{
    private static TradeCompleted Sample() => new()
    {
        EventId = Guid.NewGuid(),
        TradeId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        InstrumentId = Guid.NewGuid(),
        Symbol = "AAPL",
        Side = TradeSides.Buy,
        Quantity = 10m,
        Price = 100.50m,
        ExecutedAt = new DateTimeOffset(2026, 10, 16, 18, 30, 0, TimeSpan.Zero),
        CorrelationId = Guid.NewGuid()
    };

    [Fact]
    public void A_new_row_is_pending_and_has_no_failures()
    {
        var evt = Sample();
        var now = new DateTimeOffset(2026, 10, 16, 18, 30, 1, TimeSpan.Zero);

        var row = OutboxMessage.ForTradeCompleted(evt, now);

        Assert.Equal(evt.EventId, row.EventId);
        Assert.Equal("TradeCompleted", row.EventType);
        Assert.Equal(now, row.CreatedAt);
        Assert.Null(row.PublishedAt);
        Assert.Equal(0, row.Attempts);
        Assert.Null(row.LastError);
    }

    [Fact]
    public void The_payload_is_the_event_and_reads_back_identically()
    {
        var evt = Sample();

        var row = OutboxMessage.ForTradeCompleted(evt, DateTimeOffset.UtcNow);
        var back = ContractJson.Deserialize<TradeCompleted>(row.Payload);

        Assert.Equal(evt, back);
    }

    [Fact]
    public void The_stored_payload_is_exactly_what_the_contract_serializer_produces()
    {
        var evt = Sample();

        var row = OutboxMessage.ForTradeCompleted(evt, DateTimeOffset.UtcNow);

        Assert.Equal(ContractJson.Serialize(evt), row.Payload);
    }
}
