using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyColumnsToTrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cost_basis",
                schema: "trade",
                table: "trades",
                type: "numeric(18,8)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "request_hash",
                schema: "trade",
                table: "trades",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "ck_trades_cost_basis_non_negative",
                schema: "trade",
                table: "trades",
                sql: "cost_basis IS NULL OR cost_basis >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_trades_cost_basis_non_negative",
                schema: "trade",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "cost_basis",
                schema: "trade",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "request_hash",
                schema: "trade",
                table: "trades");
        }
    }
}
