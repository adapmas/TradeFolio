using System.Text.Json.Serialization;

namespace TradeFolio.Contracts.Events;

/// <summary>
/// Published once for every successful trade (spec section 11). The property order below is the order
/// in the JSON. <see cref="EventId"/> is the idempotency key for consumers: the same id means the same event.
/// </summary>
public sealed record TradeCompleted
{
    public const string EventTypeName = "TradeCompleted";
    public const int CurrentSchemaVersion = 1;

    public required Guid EventId { get; init; }

    /// <summary>Always "TradeCompleted". Fixed by the type so a publisher cannot get it wrong.</summary>
    public string EventType => EventTypeName;

    /// <summary>Bump when the shape changes in a way old consumers cannot read.</summary>
    public int SchemaVersion => CurrentSchemaVersion;

    public required Guid TradeId { get; init; }
    public required Guid UserId { get; init; }
    public required Guid InstrumentId { get; init; }
    public required string Symbol { get; init; }

    /// <summary>"BUY" or "SELL". Use <see cref="TradeSides"/>.</summary>
    public required string Side { get; init; }

    public required decimal Quantity { get; init; }
    public required decimal Price { get; init; }

    [JsonConverter(typeof(UtcDateTimeOffsetConverter))]
    public required DateTimeOffset ExecutedAt { get; init; }

    public required Guid CorrelationId { get; init; }
}