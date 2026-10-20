using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TradeService.Infrastructure.Persistence;

public sealed class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("instruments", t =>
        {
            t.HasCheckConstraint("ck_instruments_symbol_uppercase", "symbol = upper(symbol)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Symbol).HasColumnName("symbol").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Sector).HasColumnName("sector").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();

        builder.HasIndex(x => x.Symbol).IsUnique().HasDatabaseName("ux_instruments_symbol");

        builder.HasData(InstrumentSeed.All);
    }
}

public sealed class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("trades", t =>
        {
            t.HasCheckConstraint("ck_trades_quantity_positive", "quantity > 0");
            t.HasCheckConstraint("ck_trades_price_positive", "price > 0");
            t.HasCheckConstraint("ck_trades_side_valid", "side IN ('Buy', 'Sell')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.Side).HasColumnName("side").HasConversion<string>().HasMaxLength(4).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,8)").IsRequired();
        builder.Property(x => x.Price).HasColumnName("price").HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(x => x.ExecutedAt).HasColumnName("executed_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100).IsRequired();

        builder.HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);

        // Trade history: newest first for one user.
        builder.HasIndex(x => new { x.UserId, x.ExecutedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_trades_user_id_executed_at");

        // Instrument-specific history and analytics.
        builder.HasIndex(x => new { x.UserId, x.InstrumentId, x.ExecutedAt })
            .HasDatabaseName("ix_trades_user_id_instrument_id_executed_at");

        // Idempotency: one trade per user and key.
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_trades_user_id_idempotency_key");
    }
}

public sealed class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder.ToTable("holdings", t =>
        {
            t.HasCheckConstraint("ck_holdings_quantity_non_negative", "quantity >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,8)").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

        builder.HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);

        // One current holding per user and instrument.
        builder.HasIndex(x => new { x.UserId, x.InstrumentId })
            .IsUnique()
            .HasDatabaseName("ux_holdings_user_id_instrument_id");
    }
}

public sealed class HoldingLotConfiguration : IEntityTypeConfiguration<HoldingLot>
{
    public void Configure(EntityTypeBuilder<HoldingLot> builder)
    {
        builder.ToTable("holding_lots", t =>
        {
            t.HasCheckConstraint("ck_holding_lots_original_positive", "original_quantity > 0");
            t.HasCheckConstraint("ck_holding_lots_remaining_non_negative", "remaining_quantity >= 0");
            t.HasCheckConstraint("ck_holding_lots_remaining_within_original", "remaining_quantity <= original_quantity");
            t.HasCheckConstraint("ck_holding_lots_price_positive", "purchase_price > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.HoldingId).HasColumnName("holding_id").IsRequired();
        builder.Property(x => x.OriginalQuantity).HasColumnName("original_quantity").HasColumnType("numeric(18,8)").IsRequired();
        builder.Property(x => x.RemainingQuantity).HasColumnName("remaining_quantity").HasColumnType("numeric(18,8)").IsRequired();
        builder.Property(x => x.PurchasePrice).HasColumnName("purchase_price").HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(x => x.PurchasedAt).HasColumnName("purchased_at").HasColumnType("timestamptz").IsRequired();

        builder.HasOne<Holding>().WithMany().HasForeignKey(x => x.HoldingId).OnDelete(DeleteBehavior.Cascade);

        // FIFO traversal: oldest lot first for one holding.
        builder.HasIndex(x => new { x.HoldingId, x.PurchasedAt })
            .HasDatabaseName("ix_holding_lots_holding_id_purchased_at");
    }
}