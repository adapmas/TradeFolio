using System.Data;
using Microsoft.EntityFrameworkCore;
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
    decimal? RealizedProfitLoss = null);

public interface ITradeExecutor
{
    /// <summary>Returns null if the symbol is not a known instrument.</summary>
    Task<TradeExecuted?> BuyAsync(TradeCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Returns null if the symbol is not a known instrument.
    /// Throws InsufficientQuantityException if the user does not own enough shares.
    /// </summary>
    Task<TradeExecuted?> SellAsync(TradeCommand command, CancellationToken cancellationToken);
}

public sealed class TradeExecutor(TradeDbContext db) : ITradeExecutor
{
    public async Task<TradeExecuted?> BuyAsync(TradeCommand command, CancellationToken cancellationToken)
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
        //    The unique index on (user_id, instrument_id) makes this safe when
        //    two first BUYs arrive at the same moment.
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

        // 3. The trade record itself.
        var trade = NewTrade(command, instrumentId.Value, TradeSide.Buy, now);
        db.Trades.Add(trade);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TradeExecuted(trade.Id, symbol, trade.Side, trade.Quantity, trade.Price, trade.ExecutedAt);
    }

    public async Task<TradeExecuted?> SellAsync(TradeCommand command, CancellationToken cancellationToken)
    {
        var symbol = Normalize(command.Symbol);
        var instrumentId = await FindInstrumentIdAsync(symbol, cancellationToken);
        if (instrumentId is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        // Step 1: Read Committed plus an explicit row lock (see ADR 0002 in task 4.5).
        // If anything below throws, the transaction is rolled back and the lock released.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        // Steps 2 to 4: lock the holding row and read its quantity in ONE statement.
        // If another request holds the lock, this waits until that request commits or
        // rolls back, and then returns the UPDATED row. That is how two simultaneous
        // sells cannot both spend the same shares.
        var holding = (await db.Holdings
                .FromSqlInterpolated($"""
                    SELECT * FROM trade.holdings
                    WHERE user_id = {command.UserId} AND instrument_id = {instrumentId.Value}
                    FOR UPDATE
                    """)
                .ToListAsync(cancellationToken))
            .SingleOrDefault();

        // Step 5: reject if the user does not own enough (no holding means 0 owned).
        var available = holding?.Quantity ?? 0m;
        if (holding is null || command.Quantity > available)
        {
            throw new InsufficientQuantityException(command.Quantity, available);
        }

        // Step 6: open lots, oldest first.
        var lots = await db.HoldingLots
            .Where(l => l.HoldingId == holding.Id && l.RemainingQuantity > 0m)
            .OrderBy(l => l.PurchasedAt)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        // Step 7: the pure FIFO logic from Phase 2.
        var fifo = FifoCalculator.Sell(
            lots.Select(l => new Lot(l.Id, l.OriginalQuantity, l.RemainingQuantity, l.PurchasePrice, l.PurchasedAt)).ToList(),
            command.Quantity,
            command.Price);

        // Step 8: update the consumed lots (kept at 0 when fully used) and the holding.
        foreach (var consumption in fifo.Consumptions)
        {
            var lot = lots.Single(l => l.Id == consumption.LotId);
            lot.RemainingQuantity -= consumption.QuantityConsumed;
        }

        holding.Quantity -= command.Quantity;
        holding.UpdatedAt = now;

        // Step 9: the trade record.
        var trade = NewTrade(command, instrumentId.Value, TradeSide.Sell, now);
        db.Trades.Add(trade);

        // Step 10 (outbox event) arrives in Phase 6.

        // Step 11: commit everything together.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TradeExecuted(
            trade.Id, symbol, trade.Side, trade.Quantity, trade.Price, trade.ExecutedAt,
            fifo.CostBasis, fifo.RealizedProfitLoss);
    }

    private static string Normalize(string symbol) => symbol.Trim().ToUpperInvariant();

    private Task<Guid?> FindInstrumentIdAsync(string symbol, CancellationToken cancellationToken) =>
        db.Instruments
            .AsNoTracking()
            .Where(i => i.Symbol == symbol)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static Trade NewTrade(TradeCommand command, Guid instrumentId, TradeSide side, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = command.UserId,
        InstrumentId = instrumentId,
        Side = side,
        Quantity = command.Quantity,
        Price = command.Price,
        ExecutedAt = now,
        IdempotencyKey = command.IdempotencyKey
    };
}