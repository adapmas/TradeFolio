using TradeFolio.Contracts.Events;
using TradeFolio.Contracts;

namespace TradeService.Infrastructure.Persistence;

/// <summary>
/// One event waiting to be published (transactional outbox, spec section 11). It is written in the same
/// transaction as the trade, so a committed trade always has its event, even if RabbitMQ is down at that moment.
/// A row with <see cref="PublishedAt"/> empty is pending.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>The event's own id (also the primary key). Consumers deduplicate on it.</summary>
    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    /// <summary>The event serialized with <see cref="ContractJson"/>, stored as jsonb text and published as is.</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set by the publisher once the broker has confirmed the message. Null means pending.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>How many failed publish attempts so far.</summary>
    public int Attempts { get; set; }

    /// <summary>The error text of the latest failed attempt. Never holds secrets: only the exception message.</summary>
    public string? LastError { get; set; }

    /// <summary>Builds the pending row for a trade event. The caller adds it to the same DbContext as the trade.</summary>
    public static OutboxMessage ForTradeCompleted(TradeCompleted evt, DateTimeOffset now) => new()
    {
        EventId = evt.EventId,
        EventType = evt.EventType,
        Payload = ContractJson.Serialize(evt),
        CreatedAt = now,
        PublishedAt = null,
        Attempts = 0,
        LastError = null
    };
}