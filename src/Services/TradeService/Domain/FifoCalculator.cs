namespace TradeService.Domain;

public static class FifoCalculator
{
    /// <summary>
    /// Sells shares against open lots, oldest lot first (FIFO).
    /// Pure function: it never changes the lots passed in, it returns new values.
    /// </summary>
    public static FifoSellResult Sell(IReadOnlyList<Lot> openLots, decimal quantityToSell, decimal sellPrice)
    {
        ArgumentNullException.ThrowIfNull(openLots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantityToSell);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sellPrice);

        // 1. Only lots that still have shares, oldest first.
        //    Equal timestamps are ordered by Id so the result is always the same.
        var ordered = openLots
            .Where(lot => lot.RemainingQuantity > 0m)
            .OrderBy(lot => lot.PurchasedAt)
            .ThenBy(lot => lot.Id)
            .ToList();

        // 2. Reject the sale if there are not enough shares.
        var available = ordered.Sum(lot => lot.RemainingQuantity);
        if (quantityToSell > available)
        {
            throw new InsufficientQuantityException(quantityToSell, available);
        }

        // 3. Walk through the lots, taking shares until the sale is covered.
        var stillToSell = quantityToSell;
        var costBasis = 0m;
        var consumptions = new List<LotConsumption>();
        var remainingLots = new List<Lot>();

        foreach (var lot in ordered)
        {
            if (stillToSell == 0m)
            {
                remainingLots.Add(lot);   // sale already covered: lot untouched
                continue;
            }

            var taken = Math.Min(lot.RemainingQuantity, stillToSell);
            costBasis += taken * lot.PurchasePrice;
            consumptions.Add(new LotConsumption(lot.Id, taken, lot.PurchasePrice));
            stillToSell -= taken;

            var left = lot.RemainingQuantity - taken;
            if (left > 0m)
            {
                remainingLots.Add(lot with { RemainingQuantity = left });
            }
        }

        var proceeds = quantityToSell * sellPrice;

        return new FifoSellResult(
            QuantitySold: quantityToSell,
            CostBasis: costBasis,
            Proceeds: proceeds,
            RealizedProfitLoss: proceeds - costBasis,
            Consumptions: consumptions,
            RemainingLots: remainingLots);
    }
}