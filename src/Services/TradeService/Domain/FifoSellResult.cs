namespace TradeService.Domain;

/// <summary>How much was taken from one lot by a sale.</summary>
public sealed record LotConsumption(
    Guid LotId,
    decimal QuantityConsumed,
    decimal PurchasePrice);

/// <summary>The outcome of selling shares against a set of lots using FIFO.</summary>
public sealed record FifoSellResult(
    decimal QuantitySold,
    decimal CostBasis,
    decimal Proceeds,
    decimal RealizedProfitLoss,
    IReadOnlyList<LotConsumption> Consumptions,
    IReadOnlyList<Lot> RemainingLots);