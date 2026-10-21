using Microsoft.AspNetCore.Mvc;
using TradeFolio.Common.Errors;
using TradeService.Application;
using TradeService.Domain;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Api;

public sealed record TradeResponse(
    Guid Id,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt,
    decimal? CostBasis,
    decimal? RealizedProfitLoss);

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

        // The Idempotency-Key header is required: it makes retries safe.
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(http, ApiProblem.Validation(
                new Dictionary<string, string[]>
                {
                    ["Idempotency-Key"] = ["The Idempotency-Key header is required."]
                },
                http.TraceIdentifier));
        }

        var key = idempotencyKey.Trim();
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
        var command = new TradeCommand(userId, request.Symbol!, request.Quantity, request.Price, key);

        TradeExecuted? executed;
        try
        {
            executed = side == TradeSide.Buy
                ? await executor.BuyAsync(command, ct)
                : await executor.SellAsync(command, ct);
        }
        catch (InsufficientQuantityException ex)
        {
            return Problem(http, new ApiProblem(
                "https://api.tradefolio.local/errors/insufficient-quantity",
                "Insufficient quantity",
                422,
                http.TraceIdentifier,
                new Dictionary<string, string[]> { ["quantity"] = [ex.Message] }));
        }
        catch (IdempotencyKeyReuseException)
        {
            return Problem(http, ApiProblem.Conflict(
                "This Idempotency-Key was already used with a different request.",
                http.TraceIdentifier));
        }

        if (executed is null)
        {
            return Problem(http, ApiProblem.Validation(
                new Dictionary<string, string[]> { ["symbol"] = ["Symbol is not a known instrument."] },
                http.TraceIdentifier));
        }

        if (executed.IsReplay)
        {
            http.Response.Headers["Idempotency-Replayed"] = "true";
        }

        var response = new TradeResponse(
            executed.TradeId,
            executed.Symbol,
            executed.Side.ToString().ToUpperInvariant(),
            executed.Quantity,
            executed.Price,
            executed.ExecutedAt,
            executed.CostBasis,
            executed.RealizedProfitLoss);

        return Results.Created($"/api/v1/trades/{response.Id}", response);
    }

    private static IResult Problem(HttpContext http, ApiProblem problem) =>
        Results.Json(problem, contentType: "application/problem+json", statusCode: problem.Status);
}