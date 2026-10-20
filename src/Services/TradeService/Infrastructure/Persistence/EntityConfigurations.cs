using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TradeService.Infrastructure.Persistence;

public sealed class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("instruments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Symbol).HasColumnName("symbol").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Sector).HasColumnName("sector").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
    }
}

public sealed class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("trades");
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
    }
}

public sealed class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder.ToTable("holdings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.InstrumentId).HasColumnName("instrument_id").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,8)").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

        builder.HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class HoldingLotConfiguration : IEntityTypeConfiguration<HoldingLot>
{
    public void Configure(EntityTypeBuilder<HoldingLot> builder)
    {
        builder.ToTable("holding_lots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.HoldingId).HasColumnName("holding_id").IsRequired();
        builder.Property(x => x.OriginalQuantity).HasColumnName("original_quantity").HasColumnType("numeric(18,8)").IsRequired();
        builder.Property(x => x.RemainingQuantity).HasColumnName("remaining_quantity").HasColumnType("numeric(18,8)").IsRequired();
        builder.Property(x => x.PurchasePrice).HasColumnName("purchase_price").HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(x => x.PurchasedAt).HasColumnName("purchased_at").HasColumnType("timestamptz").IsRequired();

        builder.HasOne<Holding>().WithMany().HasForeignKey(x => x.HoldingId).OnDelete(DeleteBehavior.Cascade);
    }
}