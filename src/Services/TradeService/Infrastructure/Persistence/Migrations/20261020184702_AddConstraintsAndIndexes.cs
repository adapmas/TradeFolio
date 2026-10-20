using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConstraintsAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_holding_lots_holding_id",
                schema: "trade",
                table: "holding_lots");

            migrationBuilder.CreateIndex(
                name: "ix_trades_user_id_executed_at",
                schema: "trade",
                table: "trades",
                columns: new[] { "user_id", "executed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_trades_user_id_instrument_id_executed_at",
                schema: "trade",
                table: "trades",
                columns: new[] { "user_id", "instrument_id", "executed_at" });

            migrationBuilder.CreateIndex(
                name: "ux_trades_user_id_idempotency_key",
                schema: "trade",
                table: "trades",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_trades_price_positive",
                schema: "trade",
                table: "trades",
                sql: "price > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_trades_quantity_positive",
                schema: "trade",
                table: "trades",
                sql: "quantity > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_trades_side_valid",
                schema: "trade",
                table: "trades",
                sql: "side IN ('Buy', 'Sell')");

            migrationBuilder.CreateIndex(
                name: "ux_instruments_symbol",
                schema: "trade",
                table: "instruments",
                column: "symbol",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_instruments_symbol_uppercase",
                schema: "trade",
                table: "instruments",
                sql: "symbol = upper(symbol)");

            migrationBuilder.CreateIndex(
                name: "ux_holdings_user_id_instrument_id",
                schema: "trade",
                table: "holdings",
                columns: new[] { "user_id", "instrument_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_holdings_quantity_non_negative",
                schema: "trade",
                table: "holdings",
                sql: "quantity >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_holding_lots_holding_id_purchased_at",
                schema: "trade",
                table: "holding_lots",
                columns: new[] { "holding_id", "purchased_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_lots_original_positive",
                schema: "trade",
                table: "holding_lots",
                sql: "original_quantity > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_lots_price_positive",
                schema: "trade",
                table: "holding_lots",
                sql: "purchase_price > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_lots_remaining_non_negative",
                schema: "trade",
                table: "holding_lots",
                sql: "remaining_quantity >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_lots_remaining_within_original",
                schema: "trade",
                table: "holding_lots",
                sql: "remaining_quantity <= original_quantity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_trades_user_id_executed_at",
                schema: "trade",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "ix_trades_user_id_instrument_id_executed_at",
                schema: "trade",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "ux_trades_user_id_idempotency_key",
                schema: "trade",
                table: "trades");

            migrationBuilder.DropCheckConstraint(
                name: "ck_trades_price_positive",
                schema: "trade",
                table: "trades");

            migrationBuilder.DropCheckConstraint(
                name: "ck_trades_quantity_positive",
                schema: "trade",
                table: "trades");

            migrationBuilder.DropCheckConstraint(
                name: "ck_trades_side_valid",
                schema: "trade",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "ux_instruments_symbol",
                schema: "trade",
                table: "instruments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_instruments_symbol_uppercase",
                schema: "trade",
                table: "instruments");

            migrationBuilder.DropIndex(
                name: "ux_holdings_user_id_instrument_id",
                schema: "trade",
                table: "holdings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_holdings_quantity_non_negative",
                schema: "trade",
                table: "holdings");

            migrationBuilder.DropIndex(
                name: "ix_holding_lots_holding_id_purchased_at",
                schema: "trade",
                table: "holding_lots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_lots_original_positive",
                schema: "trade",
                table: "holding_lots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_lots_price_positive",
                schema: "trade",
                table: "holding_lots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_lots_remaining_non_negative",
                schema: "trade",
                table: "holding_lots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_lots_remaining_within_original",
                schema: "trade",
                table: "holding_lots");

            migrationBuilder.CreateIndex(
                name: "IX_holding_lots_holding_id",
                schema: "trade",
                table: "holding_lots",
                column: "holding_id");
        }
    }
}
