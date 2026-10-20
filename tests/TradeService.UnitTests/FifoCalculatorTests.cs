using TradeService.Domain;

namespace TradeService.UnitTests;

public class FifoCalculatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Ids are ordered: Id(1) sorts before Id(2).
    private static Guid Id(int n) => new(n, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static Lot MakeLot(int id, decimal quantity, decimal price, int dayOffset, decimal? remaining = null) =>
        new(Id(id), quantity, remaining ?? quantity, price, Start.AddDays(dayOffset));

    [Fact]
    public void Spec_example_BUY_10_at_100_BUY_5_at_120_SELL_12_at_150()
    {
        var lots = new[] { MakeLot(1, 10, 100, 0), MakeLot(2, 5, 120, 1) };

        var result = FifoCalculator.Sell(lots, 12, 150);

        Assert.Equal(12m, result.QuantitySold);
        Assert.Equal(1240m, result.CostBasis);
        Assert.Equal(1800m, result.Proceeds);
        Assert.Equal(560m, result.RealizedProfitLoss);
        Assert.Equal(
            new[] { new LotConsumption(Id(1), 10, 100), new LotConsumption(Id(2), 2, 120) },
            result.Consumptions.ToArray());
        Assert.Equal(
            new[] { MakeLot(2, 5, 120, 1, remaining: 3) },
            result.RemainingLots.ToArray());
    }

    [Fact]
    public void Selling_exactly_one_lot_leaves_the_next_lot_untouched()
    {
        var lots = new[] { MakeLot(1, 10, 100, 0), MakeLot(2, 5, 120, 1) };

        var result = FifoCalculator.Sell(lots, 10, 130);

        Assert.Equal(1000m, result.CostBasis);
        Assert.Equal(1300m, result.Proceeds);
        Assert.Equal(300m, result.RealizedProfitLoss);
        Assert.Equal(new[] { new LotConsumption(Id(1), 10, 100) }, result.Consumptions.ToArray());
        Assert.Equal(new[] { MakeLot(2, 5, 120, 1) }, result.RemainingLots.ToArray());
    }

    [Fact]
    public void Selling_everything_leaves_no_lots()
    {
        var lots = new[] { MakeLot(1, 10, 100, 0), MakeLot(2, 5, 120, 1) };

        var result = FifoCalculator.Sell(lots, 15, 110);

        Assert.Equal(1600m, result.CostBasis);
        Assert.Equal(1650m, result.Proceeds);
        Assert.Equal(50m, result.RealizedProfitLoss);
        Assert.Empty(result.RemainingLots);
    }

    [Fact]
    public void Selling_more_than_owned_throws_and_reports_the_quantities()
    {
        var lots = new[] { MakeLot(1, 10, 100, 0), MakeLot(2, 5, 120, 1) };

        var ex = Assert.Throws<InsufficientQuantityException>(() => FifoCalculator.Sell(lots, 16, 150));

        Assert.Equal(16m, ex.Requested);
        Assert.Equal(15m, ex.Available);
    }

    [Fact]
    public void Lots_with_the_same_timestamp_are_consumed_in_id_order()
    {
        // Listed with Id(2) first, but both bought at the same moment.
        var lots = new[] { MakeLot(2, 5, 200, 0), MakeLot(1, 5, 100, 0) };

        var result = FifoCalculator.Sell(lots, 5, 150);

        Assert.Equal(500m, result.CostBasis);
        Assert.Equal(250m, result.RealizedProfitLoss);
        Assert.Equal(new[] { new LotConsumption(Id(1), 5, 100) }, result.Consumptions.ToArray());
        Assert.Equal(new[] { MakeLot(2, 5, 200, 0) }, result.RemainingLots.ToArray());
    }

    [Fact]
    public void Partially_consuming_one_lot_keeps_its_original_quantity()
    {
        var lots = new[] { MakeLot(1, 10, 100, 0) };

        var result = FifoCalculator.Sell(lots, 4, 120);

        Assert.Equal(400m, result.CostBasis);
        Assert.Equal(480m, result.Proceeds);
        Assert.Equal(80m, result.RealizedProfitLoss);
        Assert.Equal(new[] { new LotConsumption(Id(1), 4, 100) }, result.Consumptions.ToArray());
        Assert.Equal(new[] { MakeLot(1, 10, 100, 0, remaining: 6) }, result.RemainingLots.ToArray());
    }

    [Fact]
    public void Lots_are_consumed_oldest_first_even_if_passed_in_a_different_order()
    {
        // Newest lot listed first.
        var lots = new[] { MakeLot(2, 5, 120, 1), MakeLot(1, 10, 100, 0) };

        var result = FifoCalculator.Sell(lots, 12, 150);

        Assert.Equal(1240m, result.CostBasis);
        Assert.Equal(560m, result.RealizedProfitLoss);
        Assert.Equal(
            new[] { new LotConsumption(Id(1), 10, 100), new LotConsumption(Id(2), 2, 120) },
            result.Consumptions.ToArray());
    }

    [Fact]
    public void Lots_with_nothing_remaining_are_skipped()
    {
        var lots = new[] { MakeLot(1, 10, 100, 0, remaining: 0), MakeLot(2, 5, 120, 1) };

        var result = FifoCalculator.Sell(lots, 3, 130);

        Assert.Equal(360m, result.CostBasis);
        Assert.Equal(new[] { new LotConsumption(Id(2), 3, 120) }, result.Consumptions.ToArray());
        Assert.Equal(new[] { MakeLot(2, 5, 120, 1, remaining: 2) }, result.RemainingLots.ToArray());
    }

    [Fact]
    public void Fractional_quantities_and_prices_stay_exact()
    {
        var lots = new[] { MakeLot(1, 1.5m, 100.10m, 0) };

        var result = FifoCalculator.Sell(lots, 0.25m, 110.20m);

        Assert.Equal(25.025m, result.CostBasis);
        Assert.Equal(27.55m, result.Proceeds);
        Assert.Equal(2.525m, result.RealizedProfitLoss);
        Assert.Equal(1.25m, result.RemainingLots.Single().RemainingQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_quantity_is_rejected(int quantity)
    {
        var lots = new[] { MakeLot(1, 10, 100, 0) };

        Assert.Throws<ArgumentOutOfRangeException>(() => FifoCalculator.Sell(lots, quantity, 150));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_price_is_rejected(int price)
    {
        var lots = new[] { MakeLot(1, 10, 100, 0) };

        Assert.Throws<ArgumentOutOfRangeException>(() => FifoCalculator.Sell(lots, 5, price));
    }

    // [YOU] Add your own edge case below (see the note under Step 5).
}