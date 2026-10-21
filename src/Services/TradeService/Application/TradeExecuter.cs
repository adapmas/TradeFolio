using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradeService.Domain;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Application;

public sealed record TradeCommand(
    Guid UserId,
    string Symbol,
    decimal Quantity,
    decimal Price,
    string IdempotencyKey);

public sealed record TradeExecuted(
    Guid TradeId,
    string Symbol,
    TradeSide Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt,
    decimal? CostBasis = null,
    decimal? RealizedProfitLoss = null,
    bool IsReplay = false);

/// <summary>The same Idempotency-Key was used earlier for a different request.</summary>
public sealed class IdempotencyKeyReuseException(string key)
    : Exception($"Idempotency-Key '{key}' was already used with a different request.")
{
    public string Key { get; } = key;
}

public interface ITradeExecutor
{
    /// <summary>
    /// Returns null if the symbol is not a known instrument.
    /// Throws IdempotencyKeyReuseException if the key was used for a different request.
    /// </summary>
    Task<TradeExecuted?> BuyAsync(TradeCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Returns null if the symbol is not a known instrument.
    /// Throws InsufficientQuantityException if the user does not own enough shares.
    /// Throws IdempotencyKeyReuseException if the key was used for a different request.
    /// </summary>
    Task<TradeExecuted?> SellAsync(TradeCommand command, CancellationToken cancellationToken);
}

public sealed class TradeExecutor(TradeDbContext db) : ITradeExecutor
{
    private const string IdempotencyIndexName = "ux_trades_user_id_idempotency_key";

    public Task<TradeExecuted?> BuyAsync(TradeCommand command, CancellationToken cancellationToken) =>
        ExecuteOnceAsync(command, TradeSide.Buy, BuyCoreAsync, cancellationToken);

    public Task<TradeExecuted?> SellAsync(TradeCommand command, CancellationToken cancellationToken) =>
        ExecuteOnceAsync(command, TradeSide.Sell, SellCoreAsync, cancellationToken);

    /// <summary>
    /// Wraps a trade with the idempotency rules: replay an earlier result for the same key
    /// and request, reject the key if it was used for a different request.
    /// </summary>
    private async Task<TradeExecuted?> ExecuteOnceAsync(
        TradeCommand command,
        TradeSide side,
        Func<TradeCommand, string, CancellationToken, Task<TradeExecuted?>> core,
        CancellationToken cancellationToken)
    {
        var hash = RequestFingerprint.Compute(side, command.Symbol, command.Quantity, command.Price);

        // Layer 1: an ordinary retry. The key was already used earlier.
        var previous = await FindPreviousAsync(command, hash, cancellationToken);
        if (previous is not null)
        {
            return previous;
        }

        try
        {
            return await core(command, hash, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex))
        {
            // Layer 3: a parallel request with the same key committed first and the
            // unique index stopped this one. Its transaction is already rolled back.
            db.ChangeTracker.Clear();
            var winner = await FindPreviousAsync(command, hash, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return winner;
        }
    }

    private async Task<TradeExecuted?> BuyCoreAsync(
        TradeCommand command, string requestHash, CancellationToken cancellationToken)
    {
        var symbol = Normalize(command.Symbol);
        var instrumentId = await FindInstrumentIdAsync(symbol, cancellationToken);
        if (instrumentId is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        // Everything below commits together or not at all.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        // 1. Create the holding, or add to it if it already exists.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO trade.holdings (id, user_id, instrument_id, quantity, updated_at)
            VALUES ({Guid.CreateVersion7()}, {command.UserId}, {instrumentId.Value}, {command.Quantity}, {now})
            ON CONFLICT (user_id, instrument_id)
            DO UPDATE SET quantity = trade.holdings.quantity + EXCLUDED.quantity,
                          updated_at = EXCLUDED.updated_at
            """, cancellationToken);

        var holdingId = await db.Holdings
            .AsNoTracking()
            .Where(h => h.UserId == command.UserId && h.InstrumentId == instrumentId.Value)
            .Select(h => h.Id)
            .SingleAsync(cancellationToken);

        // 2. A BUY always creates one new FIFO lot.
        db.HoldingLots.Add(new HoldingLot
        {
            Id = Guid.CreateVersion7(),
            HoldingId = holdingId,
            OriginalQuantity = command.Quantity,
            RemainingQuantity = command.Quantity,
            PurchasePrice = command.Price,
            PurchasedAt = now
        });

        // 3. The trade record itself. If a parallel request used the same key, the
        //    unique index rejects this insert and everything above is rolled back.
        var trade = NewTrade(command, instrumentId.Value, TradeSide.Buy, now, requestHash, costBasis: null);
        db.Trades.Add(trade);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TradeExecuted(trade.Id, symbol, trade.Side, trade.Quantity, trade.Price, trade.ExecutedAt);
    }

    private async Task<TradeExecuted?> SellCoreAsync(
        TradeCommand command, string requestHash, CancellationToken cancellationToken)
    {
        var symbol = Normalize(command.Symbol);
        var instrumentId = await FindInstrumentIdAsync(symbol, cancellationToken);
        if (instrumentId is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        // Read Committed plus an explicit row lock (see ADR 0002 in task 4.5).
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        // Lock the holding row and read its quantity in ONE statement.
        // A competing request waits here until we commit or roll back, then reads the UPDATED row.
        var holding = (await db.Holdings
                .FromSqlInterpolated($"""
                    SELECT * FROM trade.holdings
                    WHERE user_id = {command.UserId} AND instrument_id = {instrumentId.Value}
                    FOR UPDATE
                    """)
                .ToListAsync(cancellationToken))
            .SingleOrDefault();

        // Layer 2: a duplicate that waited on the lock must get the first request's
        // result, not "insufficient quantity" because that request already sold the shares.
        var concurrent = await FindPreviousAsync(command, requestHash, cancellationToken);
        if (concurrent is not null)
        {
            return concurrent; // leaving without Commit rolls the transaction back
        }

        var available = holding?.Quantity ?? 0m;
        if (holding is null || command.Quantity > available)
        {
            throw new InsufficientQuantityException(command.Quantity, available);
        }

        // Open lots, oldest first.
        var lots = await db.HoldingLots
            .Where(l => l.HoldingId == holding.Id && l.RemainingQuantity > 0m)
            .OrderBy(l => l.PurchasedAt)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        // The pure FIFO logic from Phase 2.
        var fifo = FifoCalculator.Sell(
            lots.Select(l => new Lot(l.Id, l.OriginalQuantity, l.RemainingQuantity, l.PurchasePrice, l.PurchasedAt)).ToList(),
            command.Quantity,
            command.Price);

        foreach (var consumption in fifo.Consumptions)
        {
            var lot = lots.Single(l => l.Id == consumption.LotId);
            lot.RemainingQuantity -= consumption.QuantityConsumed;
        }

        holding.Quantity -= command.Quantity;
        holding.UpdatedAt = now;

        var trade = NewTrade(command, instrumentId.Value, TradeSide.Sell, now, requestHash, fifo.CostBasis);
        db.Trades.Add(trade);

        // The outbox event arrives in Phase 6.

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TradeExecuted(
            trade.Id, symbol, trade.Side, trade.Quantity, trade.Price, trade.ExecutedAt,
            fifo.CostBasis, fifo.RealizedProfitLoss);
    }

    /// <summary>
    /// Looks for an earlier trade with the same user and Idempotency-Key.
    /// Same request: returns it as a replay. Different request: throws.
    /// </summary>
    private async Task<TradeExecuted?> FindPreviousAsync(
        TradeCommand command, string requestHash, CancellationToken cancellationToken)
    {
        var row = await (
                from t in db.Trades.AsNoTracking()
                join i in db.Instruments on t.InstrumentId equals i.Id
                where t.UserId == command.UserId && t.IdempotencyKey == command.IdempotencyKey
                select new
                {
                    t.Id,
                    i.Symbol,
                    t.Side,
                    t.Quantity,
                    t.Price,
                    t.ExecutedAt,
                    t.CostBasis,
                    t.RequestHash
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        if (row.RequestHash != requestHash)
        {
            throw new IdempotencyKeyReuseException(command.IdempotencyKey);
        }

        decimal? realizedProfitLoss = row.CostBasis is null ? null : row.Quantity * row.Price - row.CostBasis;

        return new TradeExecuted(
            row.Id, row.Symbol, row.Side, row.Quantity, row.Price, row.ExecutedAt,
            row.CostBasis, realizedProfitLoss, IsReplay: true);
    }

    private static bool IsIdempotencyKeyViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: IdempotencyIndexName
        };

    private static string Normalize(string symbol) => symbol.Trim().ToUpperInvariant();

    private Task<Guid?> FindInstrumentIdAsync(string symbol, CancellationToken cancellationToken) =>
        db.Instruments
            .AsNoTracking()
            .Where(i => i.Symbol == symbol)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static Trade NewTrade(
        TradeCommand command, Guid instrumentId, TradeSide side, DateTimeOffset now,
        string requestHash, decimal? costBasis) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = command.UserId,
        InstrumentId = instrumentId,
        Side = side,
        Quantity = command.Quantity,
        Price = command.Price,
        ExecutedAt = now,
        IdempotencyKey = command.IdempotencyKey,
        RequestHash = requestHash,
        CostBasis = costBasis
    };
}