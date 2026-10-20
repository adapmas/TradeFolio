using Microsoft.EntityFrameworkCore;

namespace TradeService.Infrastructure.Persistence;

public sealed class TradeDbContext(DbContextOptions<TradeDbContext> options) : DbContext(options)
{
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<HoldingLot> HoldingLots => Set<HoldingLot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("trade");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradeDbContext).Assembly);
    }
}