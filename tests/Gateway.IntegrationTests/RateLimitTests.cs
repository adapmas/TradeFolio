using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Gateway.IntegrationTests;

/// <summary>A tiny real HTTP server that answers instantly and reports what it was sent.</summary>
internal sealed class StubDownstream : IAsyncDisposable
{
    private readonly WebApplication _app;

    private StubDownstream(WebApplication app, string address)
    {
        _app = app;
        Address = address;
    }

    public string Address { get; }

    public static async Task<StubDownstream> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");   // port 0: the OS picks a free one
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.Run(context =>
        {
            // Echo what arrived, so tests can check what the gateway forwarded.
            context.Response.Headers["X-Seen-Correlation-Id"] = context.Request.Headers["X-Correlation-Id"].ToString();
            context.Response.Headers["X-Seen-Authorization"] = context.Request.Headers.Authorization.ToString();
            context.Response.Headers["X-Seen-Path"] = context.Request.Path + context.Request.QueryString.ToString();
            return context.Response.WriteAsync("ok");
        });

        await app.StartAsync();
        var address = app.Urls.First();
        return new StubDownstream(app, address.EndsWith('/') ? address : address + "/");
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}

/// <summary>The real gateway in memory, with its downstream pointed at a given address.</summary>
internal sealed class StubbedGatewayFactory : WebApplicationFactory<Program>
{
    private readonly string _downstreamAddress;

    public StubbedGatewayFactory(string downstreamAddress)
    {
        _downstreamAddress = downstreamAddress;
        Environment.SetEnvironmentVariable("Jwt__SigningKey", GatewayFactory.SigningKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", GatewayFactory.SigningKey);

        // Per-instance setting, so tests running in parallel cannot overwrite each other.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:trade:Destinations:tradeservice:Address"] = _downstreamAddress
            }));

        builder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter("Yarp", LogLevel.Error);
        });
    }
}

public class RateLimitTests
{
    [Fact]
    public async Task Login_is_limited_after_a_short_burst()
    {
        await using var downstream = await StubDownstream.StartAsync();
        await using var factory = new StubbedGatewayFactory(downstream.Address);
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 8; i++)
        {
            statuses.Add(await PostLoginAsync(client));
        }

        Assert.All(statuses.Take(5), s => Assert.Equal(HttpStatusCode.OK, s));      // the burst allowance
        Assert.All(statuses.Skip(5), s => Assert.Equal(HttpStatusCode.TooManyRequests, s));
    }

    [Fact]
    public async Task A_refused_request_explains_itself()
    {
        await using var downstream = await StubDownstream.StartAsync();
        await using var factory = new StubbedGatewayFactory(downstream.Address);
        using var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            await PostLoginAsync(client);
        }

        using var response = await client.PostAsync(
            "/api/v1/auth/login", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var retryAfter = response.Headers.GetValues("Retry-After").Single();
        Assert.True(int.Parse(retryAfter, CultureInfo.InvariantCulture) >= 1);

        Assert.True(response.Headers.Contains("X-Correlation-Id"));

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.EndsWith("/rate-limited", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(429, doc.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task One_noisy_user_is_limited_without_affecting_another_user()
    {
        await using var downstream = await StubDownstream.StartAsync();
        await using var factory = new StubbedGatewayFactory(downstream.Address);
        using var noisy = ClientFor(factory, Guid.NewGuid());
        using var quiet = ClientFor(factory, Guid.NewGuid());

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 80; i++)
        {
            statuses.Add(await GetStatusAsync(noisy, "/api/v1/trades"));
        }

        Assert.All(statuses.Take(50), s => Assert.Equal(HttpStatusCode.OK, s));
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);

        // A different user has their own full bucket.
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(quiet, "/api/v1/trades"));
    }

    [Fact]
    public async Task A_flood_of_requests_without_a_token_is_limited_too()
    {
        await using var downstream = await StubDownstream.StartAsync();
        await using var factory = new StubbedGatewayFactory(downstream.Address);
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 80; i++)
        {
            statuses.Add(await GetStatusAsync(client, "/api/v1/trades"));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, statuses[0]);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task The_health_endpoint_is_never_limited()
    {
        await using var downstream = await StubDownstream.StartAsync();
        await using var factory = new StubbedGatewayFactory(downstream.Address);
        using var client = factory.CreateClient();

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(client, "/health"));
        }
    }

    private static async Task<HttpStatusCode> PostLoginAsync(HttpClient client)
    {
        using var response = await client.PostAsync(
            "/api/v1/auth/login", new StringContent("{}", Encoding.UTF8, "application/json"));
        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> GetStatusAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        return response.StatusCode;
    }

    private static HttpClient ClientFor(StubbedGatewayFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(GatewayFactory.SigningKey, userId));
        return client;
    }
}

/// <summary>What the gateway actually passes on to the service (closes the gap noted in task 5.5).</summary>
public class ProxyForwardingTests
{
    [Fact]
    public async Task The_token_path_and_correlation_id_reach_the_service()
    {
        await using var downstream = await StubDownstream.StartAsync();
        await using var factory = new StubbedGatewayFactory(downstream.Address);
        var token = TestTokens.Create(GatewayFactory.SigningKey, Guid.NewGuid());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var id = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/trades?page=2&pageSize=5");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", id);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, Header(response, "X-Seen-Correlation-Id"));
        Assert.Equal($"Bearer {token}", Header(response, "X-Seen-Authorization"));
        Assert.Equal("/api/v1/trades?page=2&pageSize=5", Header(response, "X-Seen-Path"));
        Assert.Equal(id, Header(response, "X-Correlation-Id"));
    }

    [Fact]
    public async Task A_made_up_correlation_id_is_replaced_before_it_reaches_the_service()
    {
        await using var downstream = await StubDownstream.StartAsync();
        await using var factory = new StubbedGatewayFactory(downstream.Address);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/instruments");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "abc; DROP TABLE trades;--");

        using var response = await client.SendAsync(request);

        var seen = Header(response, "X-Seen-Correlation-Id");
        Assert.True(Guid.TryParse(seen, out _));
        Assert.Equal(seen, Header(response, "X-Correlation-Id"));
    }

    [Fact]
    public async Task A_public_request_reaches_the_service_without_any_authorization()
    {
        await using var downstream = await StubDownstream.StartAsync();
        await using var factory = new StubbedGatewayFactory(downstream.Address);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/instruments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("", Header(response, "X-Seen-Authorization"));
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.GetValues(name).Single();
}