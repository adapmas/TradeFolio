namespace TradeService.Infrastructure.Persistence;

public enum TradeSide
{
    Buy,
    Sell
}

public sealed class Instrument
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
}

public sealed class Trade
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid InstrumentId { get; set; }
    public TradeSide Side { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public decimal? CostBasis { get; set; }
}

public sealed class Holding
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid InstrumentId { get; set; }
    public decimal Quantity { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class HoldingLot
{
    public Guid Id { get; set; }
    public Guid HoldingId { get; set; }
    public decimal OriginalQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTimeOffset PurchasedAt { get; set; }
}