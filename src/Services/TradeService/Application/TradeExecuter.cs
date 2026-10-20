using Microsoft.EntityFrameworkCore;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Application;

public sealed record BuyCommand(
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
    DateTimeOffset ExecutedAt);

public interface ITradeExecutor
{
    /// <summary>Returns null if the symbol is not a known instrument.</summary>
    Task<TradeExecuted?> BuyAsync(BuyCommand command, CancellationToken cancellationToken);
}

public sealed class TradeExecutor(TradeDbContext db) : ITradeExecutor
{
    public async Task<TradeExecuted?> BuyAsync(BuyCommand command, CancellationToken cancellationToken)
    {
        var symbol = command.Symbol.Trim().ToUpperInvariant();

        var instrumentId = await db.Instruments
            .AsNoTracking()
            .Where(i => i.Symbol == symbol)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (instrumentId is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        // Everything below commits together or not at all.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

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
        var trade = new Trade
        {
            Id = Guid.CreateVersion7(),
            UserId = command.UserId,
            InstrumentId = instrumentId.Value,
            Side = TradeSide.Buy,
            Quantity = command.Quantity,
            Price = command.Price,
            ExecutedAt = now,
            IdempotencyKey = command.IdempotencyKey
        };
        db.Trades.Add(trade);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TradeExecuted(trade.Id, symbol, trade.Side, trade.Quantity, trade.Price, trade.ExecutedAt);
    }
}