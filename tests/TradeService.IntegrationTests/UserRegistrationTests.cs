using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradeService.Application;
using TradeService.Infrastructure.Persistence;
using Xunit;

namespace TradeService.IntegrationTests;

[Collection("postgres")]
public class UserRegistrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _db;
    private readonly PasswordHasher<User> _hasher = new();

    public UserRegistrationTests(PostgresFixture db) => _db = db;

    public Task InitializeAsync() => _db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // FR-01: passwords are never stored in plain text.
    [Fact]
    public async Task Password_is_stored_as_a_hash_never_as_plain_text()
    {
        const string password = "Sup3rSecret99";

        var user = await RegisterAsync("alice@example.com", password);
        Assert.NotNull(user);

        var storedHash = await ReadHashAsync(user!.Id);

        Assert.NotEqual(password, storedHash);
        Assert.DoesNotContain(password, storedHash);
        Assert.Equal(PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(user, storedHash, password));
        Assert.Equal(PasswordVerificationResult.Failed,
            _hasher.VerifyHashedPassword(user, storedHash, "WrongPassword1"));
    }

    [Fact]
    public async Task Email_is_stored_trimmed_and_lowercase()
    {
        var user = await RegisterAsync("  Carol@Example.COM ", "Sup3rSecret99");

        Assert.NotNull(user);
        Assert.Equal("carol@example.com", user!.Email);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_ignoring_case_and_whitespace()
    {
        var first = await RegisterAsync("bob@example.com", "Sup3rSecret99");
        var second = await RegisterAsync("  BOB@Example.COM ", "An0therSecret99");

        Assert.NotNull(first);
        Assert.Null(second);

        await using var ctx = _db.CreateDbContext();
        Assert.Equal(1, await ctx.Users.AsNoTracking().CountAsync(u => u.Email == "bob@example.com"));
    }

    [Fact]
    public async Task Two_simultaneous_registrations_with_the_same_email_create_one_user()
    {
        var results = await Task.WhenAll(
            RegisterAsync("dave@example.com", "Sup3rSecret99"),
            RegisterAsync("DAVE@example.com", "Sup3rSecret99"));

        Assert.Equal(1, results.Count(u => u is not null));

        await using var ctx = _db.CreateDbContext();
        Assert.Equal(1, await ctx.Users.AsNoTracking().CountAsync(u => u.Email == "dave@example.com"));
    }

    private async Task<User?> RegisterAsync(string email, string password)
    {
        await using var ctx = _db.CreateDbContext();
        var registrar = new UserRegistrar(ctx, _hasher);
        return await registrar.RegisterAsync(email, password, CancellationToken.None);
    }

    private async Task<string> ReadHashAsync(Guid userId)
    {
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT password_hash FROM trade.users WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", userId);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }
}