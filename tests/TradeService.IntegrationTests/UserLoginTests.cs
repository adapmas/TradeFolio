using Microsoft.AspNetCore.Identity;
using Npgsql;
using TradeService.Application;
using TradeService.Infrastructure.Persistence;
using Xunit;

namespace TradeService.IntegrationTests;

[Collection("postgres")]
public class UserLoginTests : IAsyncLifetime
{
    private const string Password = "Sup3rSecret99";

    private readonly PostgresFixture _db;
    private readonly PasswordHasher<User> _hasher = new();

    public UserLoginTests(PostgresFixture db) => _db = db;

    public Task InitializeAsync() => _db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Correct_credentials_return_the_user()
    {
        var registered = await RegisterAsync("alice@example.com");

        var user = await AuthenticateAsync("alice@example.com", Password);

        Assert.NotNull(user);
        Assert.Equal(registered.Id, user!.Id);
    }

    [Fact]
    public async Task Email_is_matched_ignoring_case_and_whitespace()
    {
        await RegisterAsync("alice@example.com");

        Assert.NotNull(await AuthenticateAsync("  ALICE@Example.COM ", Password));
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        await RegisterAsync("alice@example.com");

        Assert.Null(await AuthenticateAsync("alice@example.com", "WrongPassword1"));
    }

    [Fact]
    public async Task Unknown_email_is_rejected()
    {
        Assert.Null(await AuthenticateAsync("nobody@example.com", Password));
    }

    [Fact]
    public async Task Disabled_user_cannot_log_in()
    {
        var registered = await RegisterAsync("alice@example.com");
        await SetStatusAsync(registered.Id, "Disabled");

        Assert.Null(await AuthenticateAsync("alice@example.com", Password));
    }

    private async Task<User> RegisterAsync(string email)
    {
        await using var ctx = _db.CreateDbContext();
        var registrar = new UserRegistrar(ctx, _hasher);
        return (await registrar.RegisterAsync(email, Password, CancellationToken.None))!;
    }

    private async Task<User?> AuthenticateAsync(string email, string password)
    {
        await using var ctx = _db.CreateDbContext();
        var authenticator = new UserAuthenticator(ctx, _hasher);
        return await authenticator.AuthenticateAsync(email, password, CancellationToken.None);
    }

    private async Task SetStatusAsync(Guid userId, string status)
    {
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("UPDATE trade.users SET status = @s WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("s", status);
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync();
    }
}