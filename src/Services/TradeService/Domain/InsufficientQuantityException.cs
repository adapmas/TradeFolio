using System.Globalization;

namespace TradeService.Domain;

public sealed class InsufficientQuantityException(decimal requested, decimal available)
    : Exception($"Cannot sell {Format(requested)}; only {Format(available)} available.")
{
    public decimal Requested { get; } = requested;
    public decimal Available { get; } = available;

    private static string Format(decimal value) =>
        value.ToString("0.########", CultureInfo.InvariantCulture);
}