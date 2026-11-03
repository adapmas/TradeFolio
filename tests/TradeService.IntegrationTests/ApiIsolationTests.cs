using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TradeService.Api;
using TradeService.Application;
using TradeService.Infrastructure.Persistence;
using Xunit;

namespace TradeService.IntegrationTests;

[Collection("postgres")]
public class ApiIsolationTests : IAsyncLifetime
{
    private const string Password = "Sup3rSecret99";

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private readonly PostgresFixture _db;

    public ApiIsolationTests(PostgresFixture db) => _db = db;

    public Task InitializeAsync() => _db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- Requests without a valid token (acceptance test 14, FR-02) ----------

    [Theory]
    [InlineData("GET", "/api/v1/trades")]
    [InlineData("GET", "/api/v1/trades/00000000-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/v1/holdings")]
    [InlineData("GET", "/api/v1/users/me")]
    [InlineData("POST", "/api/v1/trades")]
    public async Task Protected_endpoints_return_401_without_a_token(string method, string url)
    {
        using var client = NewClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { symbol = "AAPL", side = "BUY", quantity = 1, price = 1 });
        }

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_correctly_signed_token_is_accepted()
    {
        // The control for the rejection tests below: same helper, valid token, 200.
        var token = IssueToken(TradeApiFactory.SigningKey, DateTimeOffset.UtcNow);
        using var client = NewClient(token);

        using var response = await client.GetAsync("/api/v1/trades");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_garbage_token_is_rejected() => await AssertRejectedAsync("this.is.not-a-token");

    [Fact]
    public async Task A_tampered_token_is_rejected()
    {
        var token = await RegisterAndLoginAsync("alice@example.com");
        var parts = token.Split('.');
        var tampered = string.Join('.', parts[0], parts[1], new string('A', parts[2].Length));

        await AssertRejectedAsync(tampered);
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_rejected() =>
        await AssertRejectedAsync(IssueToken(new string('z', 40), DateTimeOffset.UtcNow));

    [Fact]
    public async Task An_expired_token_is_rejected() =>
        await AssertRejectedAsync(IssueToken(TradeApiFactory.SigningKey, DateTimeOffset.UtcNow.AddHours(-2)));

    // ---------- Register and log in (acceptance test 13, FR-01, FR-02) ----------

    [Fact]
    public async Task A_user_can_register_log_in_and_read_their_profile()
    {
        var token = await RegisterAndLoginAsync("alice@example.com");
        using var client = NewClient(token);

        var me = await client.GetFromJsonAsync<UserProfileResponse>("/api/v1/users/me");

        Assert.NotNull(me);
        Assert.Equal("alice@example.com", me!.Email);
        Assert.Equal("Active", me.Status);
    }

    [Fact]
    public async Task Register_response_never_contains_password_data()
    {
        using var client = NewClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new { email = "alice@example.com", password = Password });
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, json);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_is_rejected_ignoring_case()
    {
        await RegisterAndLoginAsync("alice@example.com");
        using var client = NewClient();

        using var again = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new { email = "ALICE@Example.com", password = Password });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_answer()
    {
        await RegisterAndLoginAsync("alice@example.com");
        using var client = NewClient();

        using var wrongPassword = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "alice@example.com", password = "WrongPassword1" });
        using var unknownEmail = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = "nobody@example.com", password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await ProblemAsync(wrongPassword), await ProblemAsync(unknownEmail));
    }

    // ---------- User isolation (acceptance test 14, FR-03) ----------

    [Fact]
    public async Task User_cannot_read_list_or_trade_against_another_users_data()
    {
        var symbol = await new RaceHarness(_db).GetSymbolAsync();
        using var alice = NewClient(await RegisterAndLoginAsync("alice@example.com"));
        using var bob = NewClient(await RegisterAndLoginAsync("bob@example.com"));

        // Alice owns 10 shares.
        using var buy = await PostTradeAsync(alice, symbol, "BUY", 10m, 100m);
        Assert.Equal(HttpStatusCode.Created, buy.StatusCode);
        var aliceTrade = (await buy.Content.ReadFromJsonAsync<TradeResponse>())!;

        // READ: Alice sees her trade; Bob gets 404, the same answer as for a trade that does not exist.
        using var own = await alice.GetAsync($"/api/v1/trades/{aliceTrade.Id}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        using var foreign = await bob.GetAsync($"/api/v1/trades/{aliceTrade.Id}");
        using var missing = await bob.GetAsync($"/api/v1/trades/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(await ProblemAsync(missing), await ProblemAsync(foreign));

        // LIST: Bob sees nothing of Alice's; Alice still sees her own.
        Assert.Equal(0, await TotalAsync(bob, "/api/v1/trades"));
        Assert.Equal(0, await TotalAsync(bob, "/api/v1/holdings"));
        Assert.Equal(1, await TotalAsync(alice, "/api/v1/trades"));
        Assert.Equal(1, await TotalAsync(alice, "/api/v1/holdings"));

        // TRADE: Bob cannot spend Alice's shares (he owns none).
        using var oversell = await PostTradeAsync(bob, symbol, "SELL", 5m, 150m);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, oversell.StatusCode);

        // Alice's position is untouched.
        Assert.Equal(10m, await FirstHoldingQuantityAsync(alice));
        Assert.Equal(1, await TotalAsync(alice, "/api/v1/trades"));
    }

    [Fact]
    public async Task The_old_user_id_header_is_not_trusted()
    {
        var symbol = await new RaceHarness(_db).GetSymbolAsync();
        using var alice = NewClient(await RegisterAndLoginAsync("alice@example.com"));
        using var bob = NewClient(await RegisterAndLoginAsync("bob@example.com"));

        using var buy = await PostTradeAsync(alice, symbol, "BUY", 10m, 100m);
        Assert.Equal(HttpStatusCode.Created, buy.StatusCode);

        var aliceMe = await alice.GetFromJsonAsync<UserProfileResponse>("/api/v1/users/me");
        bob.DefaultRequestHeaders.Add("X-User-Id", aliceMe!.Id.ToString());

        // Bob's token wins; the header naming Alice changes nothing.
        Assert.Equal(0, await TotalAsync(bob, "/api/v1/trades"));
        using var oversell = await PostTradeAsync(bob, symbol, "SELL", 5m, 150m);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, oversell.StatusCode);
    }

    [Fact]
    public async Task The_same_idempotency_key_used_by_two_users_does_not_collide()
    {
        var symbol = await new RaceHarness(_db).GetSymbolAsync();
        using var alice = NewClient(await RegisterAndLoginAsync("alice@example.com"));
        using var bob = NewClient(await RegisterAndLoginAsync("bob@example.com"));
        var key = "shared-key-" + Guid.NewGuid();

        using var a = await PostTradeAsync(alice, symbol, "BUY", 1m, 100m, key);
        using var b = await PostTradeAsync(bob, symbol, "BUY", 1m, 100m, key);

        Assert.Equal(HttpStatusCode.Created, a.StatusCode);
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);
        var tradeA = (await a.Content.ReadFromJsonAsync<TradeResponse>())!;
        var tradeB = (await b.Content.ReadFromJsonAsync<TradeResponse>())!;
        Assert.NotEqual(tradeA.Id, tradeB.Id);
        Assert.False(a.Headers.Contains("Idempotency-Replayed"));
        Assert.False(b.Headers.Contains("Idempotency-Replayed"));
    }

    // ---------- helpers ----------

    private HttpClient NewClient(string? token = null)
    {
        var client = _db.Api.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private async Task<string> RegisterAndLoginAsync(string email)
    {
        using var client = NewClient();

        using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    private static async Task<HttpResponseMessage> PostTradeAsync(
        HttpClient client, string symbol, string side, decimal quantity, decimal price, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/trades")
        {
            Content = JsonContent.Create(new { symbol, side, quantity, price })
        };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

        private static async Task<int> TotalAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return Prop(doc.RootElement, "total").GetInt32();
    }

    private static async Task<decimal> FirstHoldingQuantityAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/holdings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = Prop(doc.RootElement, "items");
        return Prop(items[0], "quantity").GetDecimal();
    }

    // Finds a property by the start of its name, ignoring case, and lists the real names if it is missing.
    private static JsonElement Prop(JsonElement element, string namePrefix)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        var names = string.Join(", ", element.EnumerateObject().Select(p => p.Name));
        throw new InvalidOperationException($"No property starting with '{namePrefix}'. Available: {names}");
    }

    // The error type and title, so two answers can be compared without their trace ids.
    private static async Task<string> ProblemAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var type = doc.RootElement.GetProperty("type").GetString();
        var title = doc.RootElement.GetProperty("title").GetString();
        return $"{type}|{title}";
    }

    private async Task AssertRejectedAsync(string token)
    {
        using var client = NewClient(token);
        using var response = await client.GetAsync("/api/v1/trades");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string IssueToken(string signingKey, DateTimeOffset issuedAt)
    {
        var options = new JwtOptions { SigningKey = signingKey };
        var service = new JwtTokenService(Options.Create(options), new FixedTimeProvider(issuedAt));
        var user = new User { Id = Guid.NewGuid(), Email = "ghost@example.com" };
        return service.CreateAccessToken(user).Value;
    }
}