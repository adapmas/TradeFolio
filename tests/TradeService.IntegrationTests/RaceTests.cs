using Microsoft.EntityFrameworkCore;
using TradeService.Domain;
using TradeService.Infrastructure.Persistence;
using Xunit;

namespace TradeService.IntegrationTests;

[Collection("postgres")]
public class RaceTests : IAsyncLifetime
{
    private readonly PostgresFixture _db;
    private readonly RaceHarness _h;

    public RaceTests(PostgresFixture db)
    {
        _db = db;
        _h = new RaceHarness(db);
    }

    public Task InitializeAsync() => _db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- 1. The two-sells race, 50 times ----------

    [Fact]
    public async Task Two_concurrent_sells_never_oversell_in_50_repeated_runs()
    {
        var symbol = await _h.GetSymbolAsync();

        for (var run = 1; run <= 50; run++)
        {
            var userId = Guid.NewGuid();
            await _h.BuyAsync(userId, symbol, 10m, 100m);

            var outcomes = await _h.RaceAsync(
                e => e.SellAsync(RaceHarness.Command(userId, symbol, 7m, 150m), CancellationToken.None),
                e => e.SellAsync(RaceHarness.Command(userId, symbol, 7m, 150m), CancellationToken.None));

            var successes = outcomes.Count(o => o.Error is null);
            Assert.True(successes == 1, $"run {run}: expected exactly 1 success but got {successes}");

            var failure = outcomes.Single(o => o.Error is not null);
            Assert.True(failure.Error is InsufficientQuantityException,
                $"run {run}: expected InsufficientQuantityException but got {failure.Error}");

            await using var ctx = _db.CreateDbContext();

            var holding = await ctx.Holdings.AsNoTracking().SingleAsync(h => h.UserId == userId);
            Assert.True(holding.Quantity == 3m,
                $"run {run}: holding quantity was {holding.Quantity}, expected 3");

            var sellTrades = await ctx.Trades.AsNoTracking()
                .CountAsync(t => t.UserId == userId && t.Side == TradeSide.Sell);
            Assert.True(sellTrades == 1, $"run {run}: found {sellTrades} sell trades, expected 1");
        }
    }

    // ---------- 2. BUY and SELL at the same time ----------

    [Fact]
    public async Task Buy_and_sell_at_the_same_time_both_succeed_and_quantities_add_up()
    {
        var userId = Guid.NewGuid();
        var symbol = await _h.GetSymbolAsync();
        await _h.BuyAsync(userId, symbol, 10m, 100m);

        var outcomes = await _h.RaceAsync(
            e => e.BuyAsync(RaceHarness.Command(userId, symbol, 5m, 120m), CancellationToken.None),
            e => e.SellAsync(RaceHarness.Command(userId, symbol, 7m, 150m), CancellationToken.None));

        Assert.All(outcomes, o => Assert.True(o.Error is null, o.Error?.ToString()));

        await using var ctx = _db.CreateDbContext();

        // 10 + 5 - 7 = 8, whichever request got the lock first.
        var holding = await ctx.Holdings.AsNoTracking().SingleAsync(h => h.UserId == userId);
        Assert.Equal(8m, holding.Quantity);

        // FIFO: the SELL took 7 from the oldest lot (10 @ 100); the new 5 @ 120 lot is untouched.
        var lots = await ctx.HoldingLots.AsNoTracking()
            .Where(l => l.HoldingId == holding.Id)
            .OrderBy(l => l.PurchasedAt)
            .ToListAsync();
        Assert.Equal(2, lots.Count);
        Assert.Equal(3m, lots[0].RemainingQuantity);
        Assert.Equal(5m, lots[1].RemainingQuantity);

        var trades = await ctx.Trades.AsNoTracking().Where(t => t.UserId == userId).ToListAsync();
        Assert.Equal(3, trades.Count);
        var sell = Assert.Single(trades, t => t.Side == TradeSide.Sell);
        Assert.Equal(700m, sell.CostBasis);   // 7 * 100
    }

    // ---------- 3. Two first-BUYs for a user with no holding yet ----------

    [Fact]
    public async Task Two_first_buys_create_one_holding_with_two_lots()
    {
        var userId = Guid.NewGuid();
        var symbol = await _h.GetSymbolAsync();

        var outcomes = await _h.RaceAsync(
            e => e.BuyAsync(RaceHarness.Command(userId, symbol, 5m, 100m), CancellationToken.None),
            e => e.BuyAsync(RaceHarness.Command(userId, symbol, 5m, 110m), CancellationToken.None));

        Assert.All(outcomes, o => Assert.True(o.Error is null, o.Error?.ToString()));

        await using var ctx = _db.CreateDbContext();

        var holding = await ctx.Holdings.AsNoTracking().SingleAsync(h => h.UserId == userId);
        Assert.Equal(10m, holding.Quantity);

        var lots = await ctx.HoldingLots.AsNoTracking().Where(l => l.HoldingId == holding.Id).ToListAsync();
        Assert.Equal(2, lots.Count);
        Assert.All(lots, l => Assert.Equal(5m, l.RemainingQuantity));

        Assert.Equal(2, await ctx.Trades.AsNoTracking().CountAsync(t => t.UserId == userId));
    }

    // ---------- 4. Same Idempotency-Key, two BUYs ----------

    [Fact]
    public async Task Two_simultaneous_buys_with_the_same_key_create_one_trade()
    {
        var userId = Guid.NewGuid();
        var symbol = await _h.GetSymbolAsync();
        var key = "same-key-" + Guid.NewGuid();

        var outcomes = await _h.RaceAsync(
            e => e.BuyAsync(RaceHarness.Command(userId, symbol, 10m, 100m, key), CancellationToken.None),
            e => e.BuyAsync(RaceHarness.Command(userId, symbol, 10m, 100m, key), CancellationToken.None));

        Assert.All(outcomes, o => Assert.True(o.Error is null, o.Error?.ToString()));

        // Both callers get the same trade; exactly one of them is the replay.
        Assert.Single(outcomes.Select(o => o.Result!.TradeId).Distinct());
        Assert.Equal(1, outcomes.Count(o => !o.Result!.IsReplay));

        await using var ctx = _db.CreateDbContext();

        Assert.Equal(1, await ctx.Trades.AsNoTracking().CountAsync(t => t.UserId == userId));

        // The loser's holding update was rolled back with its transaction: 10, not 20.
        var holding = await ctx.Holdings.AsNoTracking().SingleAsync(h => h.UserId == userId);
        Assert.Equal(10m, holding.Quantity);

        var lot = await ctx.HoldingLots.AsNoTracking().SingleAsync(l => l.HoldingId == holding.Id);
        Assert.Equal(10m, lot.RemainingQuantity);
    }

    // ---------- 5. Same Idempotency-Key, two SELLs ----------

    [Fact]
    public async Task Two_simultaneous_sells_with_the_same_key_create_one_trade()
    {
        var userId = Guid.NewGuid();
        var symbol = await _h.GetSymbolAsync();
        await _h.BuyAsync(userId, symbol, 10m, 100m);
        var key = "same-key-" + Guid.NewGuid();

        var outcomes = await _h.RaceAsync(
            e => e.SellAsync(RaceHarness.Command(userId, symbol, 7m, 150m, key), CancellationToken.None),
            e => e.SellAsync(RaceHarness.Command(userId, symbol, 7m, 150m, key), CancellationToken.None));

        // The duplicate must get the first result, not "insufficient quantity".
        Assert.All(outcomes, o => Assert.True(o.Error is null, o.Error?.ToString()));
        Assert.Single(outcomes.Select(o => o.Result!.TradeId).Distinct());
        Assert.Equal(1, outcomes.Count(o => !o.Result!.IsReplay));

        await using var ctx = _db.CreateDbContext();

        var sellTrades = await ctx.Trades.AsNoTracking()
            .CountAsync(t => t.UserId == userId && t.Side == TradeSide.Sell);
        Assert.Equal(1, sellTrades);

        var holding = await ctx.Holdings.AsNoTracking().SingleAsync(h => h.UserId == userId);
        Assert.Equal(3m, holding.Quantity);
    }
}