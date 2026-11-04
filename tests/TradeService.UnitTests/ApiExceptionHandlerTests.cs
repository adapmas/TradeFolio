using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TradeService.Api;
using Xunit;

namespace TradeService.UnitTests;

public class ApiExceptionHandlerTests
{
    private static (DefaultHttpContext Context, MemoryStream Body) NewContext()
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "trace-123" };
        context.Response.Body = body;
        return (context, body);
    }

    private static ApiExceptionHandler NewHandler() => new(NullLogger<ApiExceptionHandler>.Instance);

    [Fact]
    public async Task An_unexpected_exception_becomes_a_generic_500_that_leaks_nothing()
    {
        var (context, body) = NewContext();
        var secret = new InvalidOperationException("Npgsql failed: Host=db;Password=hunter2 at TradeService.Secret()");

        var handled = await NewHandler().TryHandleAsync(context, secret, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);

        var json = Encoding.UTF8.GetString(body.ToArray());
        Assert.DoesNotContain("hunter2", json);
        Assert.DoesNotContain("Npgsql", json);
        Assert.DoesNotContain("InvalidOperationException", json);
        Assert.DoesNotContain("TradeService.Secret", json);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("trace-123", doc.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("An unexpected error occurred", doc.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData(400, "Malformed request")]
    [InlineData(413, "Request body too large")]
    [InlineData(415, "Unsupported media type")]
    public async Task A_bad_request_keeps_its_status_and_gets_a_generic_title(int status, string title)
    {
        var (context, body) = NewContext();
        var bad = new BadHttpRequestException("Failed to read parameter \"LoginRequest request\" from the request body", status);

        var handled = await NewHandler().TryHandleAsync(context, bad, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);

        var json = Encoding.UTF8.GetString(body.ToArray());
        Assert.DoesNotContain("LoginRequest", json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(title, doc.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_disconnected_caller_is_not_reported_as_a_server_error()
    {
        var (context, _) = NewContext();
        context.RequestAborted = new CancellationToken(canceled: true);

        var handled = await NewHandler().TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(499, context.Response.StatusCode);
    }
}