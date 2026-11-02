using Microsoft.EntityFrameworkCore;
using TradeService.Application;

namespace TradeService.IntegrationTests;

public sealed record RaceOutcome(TradeExecuted? Result, Exception? Error);

public sealed class RaceHarness(PostgresFixture fixture)
{
    public async Task<string> GetSymbolAsync(int skip = 0)
    {
        await using var ctx = fixture.CreateDbContext();
        return await ctx.Instruments.AsNoTracking()
            .OrderBy(i => i.Symbol)
            .Select(i => i.Symbol)
            .Skip(skip)
            .FirstAsync();
    }

    public static TradeCommand Command(
        Guid userId, string symbol, decimal quantity, decimal price, string? key = null) =>
        new(userId, symbol, quantity, price, key ?? Guid.NewGuid().ToString());

    // A plain, non-racing BUY used to set up a scenario.
    public async Task BuyAsync(Guid userId, string symbol, decimal quantity, decimal price)
    {
        await using var ctx = fixture.CreateDbContext();
        var executor = new TradeExecutor(ctx);
        await executor.BuyAsync(Command(userId, symbol, quantity, price), CancellationToken.None);
    }

    // Starts every action at the same moment. Each action gets its own DbContext
    // and executor, like separate HTTP requests, and its connection is already open.
    public async Task<RaceOutcome[]> RaceAsync(
        params Func<ITradeExecutor, Task<TradeExecuted?>>[] actions)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readies = actions
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();

        var tasks = actions
            .Select((action, i) => Task.Run(() => RunOneAsync(action, readies[i], gate.Task)))
            .ToArray();

        await Task.WhenAll(readies.Select(r => r.Task));
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    private async Task<RaceOutcome> RunOneAsync(
        Func<ITradeExecutor, Task<TradeExecuted?>> action,
        TaskCompletionSource ready,
        Task gate)
    {
        await using var ctx = fixture.CreateDbContext();
        var executor = new TradeExecutor(ctx);

        await ctx.Database.OpenConnectionAsync();
        ready.SetResult();
        await gate;

        try
        {
            var result = await action(executor);
            return new RaceOutcome(result, null);
        }
        catch (Exception ex)
        {
            return new RaceOutcome(null, ex);
        }
    }
}