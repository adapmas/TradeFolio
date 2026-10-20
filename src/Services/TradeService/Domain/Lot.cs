namespace TradeService.Domain;

/// <summary>
/// One open BUY lot: shares bought at a single price and time.
/// A lot is immutable. Selling returns new Lot values instead of changing old ones.
/// </summary>
public sealed record Lot(
    Guid Id,
    decimal OriginalQuantity,
    decimal RemainingQuantity,
    decimal PurchasePrice,
    DateTimeOffset PurchasedAt);