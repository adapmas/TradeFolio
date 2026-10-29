using Microsoft.EntityFrameworkCore;
using TradeService.Application;
using TradeService.Domain;
using TradeService.Infrastructure.Persistence;
using Xunit;

namespace TradeService.IntegrationTests;

[Collection("postgres")]
public class CoreTradeTests : IAsyncLifetime
{
    private readonly PostgresFixture _db;

    public CoreTradeTests(PostgresFixture db) => _db = db;

    public Task InitializeAsync() => _db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- Acceptance test 15: BUY creates trade, holding and FIFO lot ----------

    [Fact]
    public async Task Buy_creates_one_trade_one_holding_and_one_lot()
    {
        var userId = Guid.NewGuid();
        var symbol = await GetSymbolAsync();

        var result = await BuyAsync(userId, symbol, quantity: 10m, price: 100m);

        Assert.False(result.IsReplay);
        Assert.Equal(TradeSide.Buy, result.Side);

        await using var db = _db.CreateDbContext();

        var trades = await db.Trades.AsNoTracking().Where(t => t.UserId == userId).ToListAsync();
        var trade = Assert.Single(trades);
        Assert.Equal(TradeSide.Buy, trade.Side);
        Assert.Equal(10m, trade.Quantity);
        Assert.Equal(100m, trade.Price);

        var holdings = await db.Holdings.AsNoTracking().Where(h => h.UserId == userId).ToListAsync();
        var holding = Assert.Single(holdings);
        Assert.Equal(10m, holding.Quantity);

        var lots = await db.HoldingLots.AsNoTracking().Where(l => l.HoldingId == holding.Id).ToListAsync();
        var lot = Assert.Single(lots);
        Assert.Equal(10m, lot.OriginalQuantity);
        Assert.Equal(10m, lot.RemainingQuantity);
        Assert.Equal(100m, lot.PurchasePrice);
    }

    // ---------- Acceptance test 16: partial SELL consumes FIFO lots correctly ----------

    [Fact]
    public async Task Partial_sell_consumes_oldest_lots_first_and_matches_spec_example()
    {
        var userId = Guid.NewGuid();
        var symbol = await GetSymbolAsync();

        await BuyAsync(userId, symbol, quantity: 10m, price: 100m);
        await BuyAsync(userId, symbol, quantity: 5m, price: 120m);
        var sell = await SellAsync(userId, symbol, quantity: 12m, price: 150m);

        // Spec section 8: cost basis 10*100 + 2*120 = 1240, proceeds 1800, realized P&L 560.
        Assert.Equal(1240m, sell.CostBasis);
        Assert.Equal(560m, sell.RealizedProfitLoss);

        await using var db = _db.CreateDbContext();

        var holding = await db.Holdings.AsNoTracking().SingleAsync(h => h.UserId == userId);
        Assert.Equal(3m, holding.Quantity);

        var lots = await db.HoldingLots.AsNoTracking()
            .Where(l => l.HoldingId == holding.Id)
            .OrderBy(l => l.PurchasedAt)
            .ToListAsync();

        Assert.Equal(2, lots.Count);
        Assert.Equal(0m, lots[0].RemainingQuantity);   // the 10 @ 100 lot is fully used
        Assert.Equal(100m, lots[0].PurchasePrice);
        Assert.Equal(3m, lots[1].RemainingQuantity);   // 3 of the 5 @ 120 lot remain
        Assert.Equal(120m, lots[1].PurchasePrice);

        var sellTrade = await db.Trades.AsNoTracking()
            .SingleAsync(t => t.UserId == userId && t.Side == TradeSide.Sell);
        Assert.Equal(1240m, sellTrade.CostBasis);
        Assert.Equal(12m, sellTrade.Quantity);
        Assert.Equal(150m, sellTrade.Price);
    }

    // ---------- Acceptance test 17: oversell is rejected ----------

    [Fact]
    public async Task Oversell_is_rejected_and_leaves_the_database_unchanged()
    {
        var userId = Guid.NewGuid();
        var symbol = await GetSymbolAsync();

        await BuyAsync(userId, symbol, quantity: 10m, price: 100m);

        var ex = await Assert.ThrowsAsync<InsufficientQuantityException>(
            () => SellAsync(userId, symbol, quantity: 11m, price: 150m));
        Assert.Equal(11m, ex.Requested);
        Assert.Equal(10m, ex.Available);

        await using var db = _db.CreateDbContext();

        var holding = await db.Holdings.AsNoTracking().SingleAsync(h => h.UserId == userId);
        Assert.Equal(10m, holding.Quantity);

        var lot = await db.HoldingLots.AsNoTracking().SingleAsync(l => l.HoldingId == holding.Id);
        Assert.Equal(10m, lot.RemainingQuantity);

        // Only the original BUY exists; no SELL trade was written.
        var trades = await db.Trades.AsNoTracking().Where(t => t.UserId == userId).ToListAsync();
        var onlyTrade = Assert.Single(trades);
        Assert.Equal(TradeSide.Buy, onlyTrade.Side);
    }

    // ---------- helpers ----------

    private async Task<string> GetSymbolAsync()
    {
        await using var db = _db.CreateDbContext();
        return await db.Instruments.AsNoTracking()
            .OrderBy(i => i.Symbol)
            .Select(i => i.Symbol)
            .FirstAsync();
    }

    // One fresh DbContext per call, like one HTTP request would get.
    private async Task<TradeExecuted> BuyAsync(Guid userId, string symbol, decimal quantity, decimal price)
    {
        await using var db = _db.CreateDbContext();
        var executor = new TradeExecutor(db);
        var command = new TradeCommand(userId, symbol, quantity, price, Guid.NewGuid().ToString());
        return await executor.BuyAsync(command, CancellationToken.None)
            ?? throw new InvalidOperationException($"Unknown symbol '{symbol}'.");
    }

    private async Task<TradeExecuted> SellAsync(Guid userId, string symbol, decimal quantity, decimal price)
    {
        await using var db = _db.CreateDbContext();
        var executor = new TradeExecutor(db);
        var command = new TradeCommand(userId, symbol, quantity, price, Guid.NewGuid().ToString());
        return await executor.SellAsync(command, CancellationToken.None)
            ?? throw new InvalidOperationException($"Unknown symbol '{symbol}'.");
    }
}