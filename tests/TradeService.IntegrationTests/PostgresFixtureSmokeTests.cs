using Npgsql;
using Xunit;

namespace TradeService.IntegrationTests;

[Collection("postgres")]
public class PostgresFixtureSmokeTests : IAsyncLifetime
{
    private readonly PostgresFixture _db;

    public PostgresFixtureSmokeTests(PostgresFixture db) => _db = db;

    public Task InitializeAsync() => _db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Can_insert_and_read_a_holding_in_real_postgres()
    {
        var holdingId = await InsertHoldingAsync(quantity: 10m);

        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT quantity FROM trade.holdings WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", holdingId);

        var quantity = (decimal)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(10m, quantity);
    }

    [Fact]
    public async Task Each_test_starts_with_a_clean_database_first_run()
    {
        Assert.Equal(0, await CountHoldingsAsync());
        await InsertHoldingAsync(quantity: 1m);
        Assert.Equal(1, await CountHoldingsAsync());
    }

    [Fact]
    public async Task Each_test_starts_with_a_clean_database_second_run()
    {
        Assert.Equal(0, await CountHoldingsAsync());
        await InsertHoldingAsync(quantity: 1m);
        Assert.Equal(1, await CountHoldingsAsync());
    }

    private async Task<Guid> InsertHoldingAsync(decimal quantity)
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO trade.holdings (id, user_id, instrument_id, quantity, updated_at)
            SELECT @id, @userId, id, @quantity, now() FROM trade.instruments LIMIT 1
            """, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("userId", Guid.NewGuid());
        cmd.Parameters.AddWithValue("quantity", quantity);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<long> CountHoldingsAsync()
    {
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM trade.holdings", conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}