using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TradeService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedInstruments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "trade",
                table: "instruments",
                columns: new[] { "id", "currency", "name", "sector", "symbol" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000001"), "USD", "Apple Inc.", "Technology", "AAPL" },
                    { new Guid("a0000000-0000-0000-0000-000000000002"), "USD", "Microsoft Corporation", "Technology", "MSFT" },
                    { new Guid("a0000000-0000-0000-0000-000000000003"), "USD", "NVIDIA Corporation", "Technology", "NVDA" },
                    { new Guid("a0000000-0000-0000-0000-000000000004"), "USD", "Alphabet Inc.", "Communication Services", "GOOGL" },
                    { new Guid("a0000000-0000-0000-0000-000000000005"), "USD", "Amazon.com Inc.", "Consumer Discretionary", "AMZN" },
                    { new Guid("a0000000-0000-0000-0000-000000000006"), "USD", "Tesla Inc.", "Consumer Discretionary", "TSLA" },
                    { new Guid("a0000000-0000-0000-0000-000000000007"), "USD", "JPMorgan Chase & Co.", "Financials", "JPM" },
                    { new Guid("a0000000-0000-0000-0000-000000000008"), "USD", "Johnson & Johnson", "Health Care", "JNJ" },
                    { new Guid("a0000000-0000-0000-0000-000000000009"), "USD", "Exxon Mobil Corporation", "Energy", "XOM" },
                    { new Guid("a0000000-0000-0000-0000-000000000010"), "USD", "The Coca-Cola Company", "Consumer Staples", "KO" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "trade",
                table: "instruments",
                keyColumn: "id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "trade",
                table: "instruments",
                keyColumn: "id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "trade",
                table: "instruments",
                keyColumn: "id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "trade",
                table: "instruments",
                keyColumn: "id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "trade",
                table: "instruments",
                keyColumn: "id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "trade",
                table: "instruments",
                keyColumn: "id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                schema: "trade",
                table: "instruments",
                keyColumn: "id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                schema: "trade",
                table: "instruments",
                keyColumn: "id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                schema: "trade",
                table: "instruments",
                keyColumn: "id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                schema: "trade",
                table: "instruments",
                keyColumn: "id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"));
        }
    }
}
