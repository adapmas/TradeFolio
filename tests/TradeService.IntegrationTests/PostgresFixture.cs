using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using TradeService.Infrastructure.Persistence;
using Xunit;

namespace TradeService.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public TradeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TradeDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new TradeDbContext(options);
    }

    // Wipes test data but keeps the seeded instruments.
    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "TRUNCATE TABLE trade.trades, trade.holding_lots, trade.holdings;", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}