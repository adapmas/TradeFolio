using Microsoft.EntityFrameworkCore;
using TradeService.Application;
using TradeService.Domain;
using TradeService.Infrastructure.Persistence;
using Xunit;

namespace TradeService.IntegrationTests;

[Collection("postgres")]
public class ConcurrencyTests : IAsyncLifetime
{
    private readonly PostgresFixture _db;

    public ConcurrencyTests(PostgresFixture db) => _db = db;

    public Task InitializeAsync() => _db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record Outcome(TradeExecuted? Result, Exception? Error);

    // Acceptance test 18 / FR-06: two simultaneous sells cannot oversell.
    [Fact]
    public async Task Two_simultaneous_sells_of_7_from_a_holding_of_10_cannot_both_succeed()
    {
        var userId = Guid.NewGuid();
        var symbol = await GetSymbolAsync();

        // Arrange: the user owns exactly 10 shares.
        await BuyAsync(userId, symbol, quantity: 10m, price: 100m);

        // Both requests get ready (own context, connection already open),
        // then wait at the gate so they start at the same moment.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var requestA = Task.Run(() => SellWhenGateOpensAsync(userId, symbol, 7m, 150m, readyA, gate.Task));
        var requestB = Task.Run(() => SellWhenGateOpensAsync(userId, symbol, 7m, 150m, readyB, gate.Task));

        await Task.WhenAll(readyA.Task, readyB.Task);
        gate.SetResult();                       // release both at once
        var outcomes = await Task.WhenAll(requestA, requestB);

        // Exactly one request succeeds ...
        var succeeded = outcomes.Where(o => o.Error is null).ToList();
        var failed = outcomes.Where(o => o.Error is not null).ToList();
        Assert.Single(succeeded);

        // ... and the other fails with insufficient quantity (it saw the updated 3 shares).
        var failure = Assert.Single(failed);
        var ex = Assert.IsType<InsufficientQuantityException>(failure.Error);
        Assert.Equal(7m, ex.Requested);
        Assert.Equal(3m, ex.Available);

        // The database agrees.
        await using var db = _db.CreateDbContext();

        var holding = await db.Holdings.AsNoTracking().SingleAsync(h => h.UserId == userId);
        Assert.Equal(3m, holding.Quantity);
        Assert.True(holding.Quantity >= 0m);

        var lots = await db.HoldingLots.AsNoTracking().Where(l => l.HoldingId == holding.Id).ToListAsync();
        Assert.All(lots, l => Assert.True(l.RemainingQuantity >= 0m));
        Assert.Equal(3m, lots.Sum(l => l.RemainingQuantity));

        // One BUY plus exactly one successful SELL, never two.
        var sellTrades = await db.Trades.AsNoTracking()
            .Where(t => t.UserId == userId && t.Side == TradeSide.Sell)
            .ToListAsync();
        Assert.Single(sellTrades);
        Assert.Equal(2, await db.Trades.AsNoTracking().CountAsync(t => t.UserId == userId));

        // The "one TradeCompleted outbox event" assertion is added in task 6.2,
        // when the OutboxMessages table exists.
    }

    // ---------- helpers ----------

    private async Task<Outcome> SellWhenGateOpensAsync(
        Guid userId, string symbol, decimal quantity, decimal price,
        TaskCompletionSource ready, Task gate)
    {
        await using var db = _db.CreateDbContext();
        var executor = new TradeExecutor(db);
        var command = new TradeCommand(userId, symbol, quantity, price, Guid.NewGuid().ToString());

        await db.Database.OpenConnectionAsync();   // no connection setup delay after the gate
        ready.SetResult();
        await gate;

        try
        {
            var result = await executor.SellAsync(command, CancellationToken.None);
            return new Outcome(result, null);
        }
        catch (Exception ex)
        {
            return new Outcome(null, ex);
        }
    }

    private async Task<string> GetSymbolAsync()
    {
        await using var db = _db.CreateDbContext();
        return await db.Instruments.AsNoTracking()
            .OrderBy(i => i.Symbol)
            .Select(i => i.Symbol)
            .FirstAsync();
    }

    private async Task BuyAsync(Guid userId, string symbol, decimal quantity, decimal price)
    {
        await using var db = _db.CreateDbContext();
        var executor = new TradeExecutor(db);
        var command = new TradeCommand(userId, symbol, quantity, price, Guid.NewGuid().ToString());
        await executor.BuyAsync(command, CancellationToken.None);
    }
}