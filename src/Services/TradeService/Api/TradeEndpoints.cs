using Microsoft.AspNetCore.Mvc;
using TradeFolio.Common.Errors;
using TradeService.Application;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Api;

public sealed record TradeResponse(
    Guid Id,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt);

public static class TradeEndpoints
{
    private const int MaxIdempotencyKeyLength = 100;

    public static IEndpointRouteBuilder MapTradeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/trades").WithTags("Trades");

        group.MapPost("/", CreateTrade)
            .AddEndpointFilter<ValidationFilter<CreateTradeRequest>>()
            .WithName("CreateTrade");

        return app;
    }

    private static async Task<IResult> CreateTrade(
        CreateTradeRequest? request,
        [FromHeader(Name = "X-User-Id")] string? userIdHeader,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ITradeExecutor executor,
        HttpContext http,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request); // the validation filter already rejected a missing body

        // TEMPORARY: the user id will come from the login token in Phase 5.
        if (!Guid.TryParse(userIdHeader, out var userId))
        {
            return Problem(http, ApiProblem.Validation(
                new Dictionary<string, string[]>
                {
                    ["X-User-Id"] = ["A valid X-User-Id header (a GUID) is required. This is temporary until login exists."]
                },
                http.TraceIdentifier));
        }

        // TEMPORARY: task 3.7 makes this header required and handles repeated keys.
        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey.Trim();
        if (key.Length > MaxIdempotencyKeyLength)
        {
            return Problem(http, ApiProblem.Validation(
                new Dictionary<string, string[]>
                {
                    ["Idempotency-Key"] = [$"Idempotency-Key must be at most {MaxIdempotencyKeyLength} characters."]
                },
                http.TraceIdentifier));
        }

        var side = Enum.Parse<TradeSide>(request.Side!.Trim(), ignoreCase: true);
        if (side == TradeSide.Sell)
        {
            return Problem(http, new ApiProblem(
                "https://api.tradefolio.local/errors/not-implemented",
                "SELL is not implemented yet",
                501,
                http.TraceIdentifier));
        }

        var executed = await executor.BuyAsync(
            new BuyCommand(userId, request.Symbol!, request.Quantity, request.Price, key), ct);

        if (executed is null)
        {
            return Problem(http, ApiProblem.Validation(
                new Dictionary<string, string[]> { ["symbol"] = ["Symbol is not a known instrument."] },
                http.TraceIdentifier));
        }

        var response = new TradeResponse(
            executed.TradeId,
            executed.Symbol,
            executed.Side.ToString().ToUpperInvariant(),
            executed.Quantity,
            executed.Price,
            executed.ExecutedAt);

        return Results.Created($"/api/v1/trades/{response.Id}", response);
    }

    private static IResult Problem(HttpContext http, ApiProblem problem) =>
        Results.Json(problem, contentType: "application/problem+json", statusCode: problem.Status);
}