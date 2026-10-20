using Microsoft.EntityFrameworkCore;

namespace TradeService.Infrastructure.Persistence;

public interface IInstrumentLookup
{
    Task<bool> SymbolExistsAsync(string symbol, CancellationToken cancellationToken);
}

public sealed class InstrumentLookup(TradeDbContext db) : IInstrumentLookup
{
    public Task<bool> SymbolExistsAsync(string symbol, CancellationToken cancellationToken)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        return db.Instruments.AnyAsync(i => i.Symbol == normalized, cancellationToken);
    }
}