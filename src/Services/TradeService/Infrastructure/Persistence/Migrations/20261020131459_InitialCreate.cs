using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trade");

            migrationBuilder.CreateTable(
                name: "instruments",
                schema: "trade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sector = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instruments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "holdings",
                schema: "trade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_holdings", x => x.id);
                    table.ForeignKey(
                        name: "FK_holdings_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalSchema: "trade",
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trades",
                schema: "trade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    side = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trades", x => x.id);
                    table.ForeignKey(
                        name: "FK_trades_instruments_instrument_id",
                        column: x => x.instrument_id,
                        principalSchema: "trade",
                        principalTable: "instruments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "holding_lots",
                schema: "trade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    holding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_quantity = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    remaining_quantity = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    purchase_price = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    purchased_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_holding_lots", x => x.id);
                    table.ForeignKey(
                        name: "FK_holding_lots_holdings_holding_id",
                        column: x => x.holding_id,
                        principalSchema: "trade",
                        principalTable: "holdings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_holding_lots_holding_id",
                schema: "trade",
                table: "holding_lots",
                column: "holding_id");

            migrationBuilder.CreateIndex(
                name: "IX_holdings_instrument_id",
                schema: "trade",
                table: "holdings",
                column: "instrument_id");

            migrationBuilder.CreateIndex(
                name: "IX_trades_instrument_id",
                schema: "trade",
                table: "trades",
                column: "instrument_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "holding_lots",
                schema: "trade");

            migrationBuilder.DropTable(
                name: "trades",
                schema: "trade");

            migrationBuilder.DropTable(
                name: "holdings",
                schema: "trade");

            migrationBuilder.DropTable(
                name: "instruments",
                schema: "trade");
        }
    }
}
