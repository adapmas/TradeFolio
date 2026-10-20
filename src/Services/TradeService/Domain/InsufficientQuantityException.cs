namespace TradeService.Domain;

public sealed class InsufficientQuantityException(decimal requested, decimal available)
    : Exception($"Cannot sell {requested}; only {available} available.")
{
    public decimal Requested { get; } = requested;
    public decimal Available { get; } = available;
}