using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeFolio.Common.Errors;
using TradeFolio.Common.Pagination;
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

internal sealed record TradeRow(
    Guid Id,
    string Symbol,
    TradeSide Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt,
    decimal? CostBasis);

public static class TradeEndpoints
{
    private const int MaxIdempotencyKeyLength = 100;

    public static IEndpointRouteBuilder MapTradeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/trades").WithTags("Trades");

        group.MapPost("/", CreateTrade)
            .AddEndpointFilter<ValidationFilter<CreateTradeRequest>>()
            .WithName("CreateTrade");

        group.MapGet("/", ListTrades).WithName("ListTrades");
        group.MapGet("/{id}", GetTrade).WithName("GetTrade");

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

        if (!CurrentUser.TryParse(userIdHeader, out var userId))
        {
            return CurrentUser.Missing(http);
        }

        // The Idempotency-Key header is required: it makes retries safe.
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return ApiResults.Problem(http, ApiProblem.Validation(
                new Dictionary<string, string[]>
                {
                    ["Idempotency-Key"] = ["The Idempotency-Key header is required."]
                },
                http.TraceIdentifier));
        }

        var key = idempotencyKey.Trim();
        if (key.Length > MaxIdempotencyKeyLength)
        {
            return ApiResults.Problem(http, ApiProblem.Validation(
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
            return ApiResults.Problem(http, new ApiProblem(
                "https://api.tradefolio.local/errors/insufficient-quantity",
                "Insufficient quantity",
                422,
                http.TraceIdentifier,
                new Dictionary<string, string[]> { ["quantity"] = [ex.Message] }));
        }
        catch (IdempotencyKeyReuseException)
        {
            return ApiResults.Problem(http, ApiProblem.Conflict(
                "This Idempotency-Key was already used with a different request.",
                http.TraceIdentifier));
        }

        if (executed is null)
        {
            return ApiResults.Problem(http, ApiProblem.Validation(
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

    private static async Task<IResult> ListTrades(
        int? page,
        int? pageSize,
        [FromHeader(Name = "X-User-Id")] string? userIdHeader,
        TradeDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        if (!CurrentUser.TryParse(userIdHeader, out var userId))
        {
            return CurrentUser.Missing(http);
        }

        var request = PageRequest.Create(page, pageSize);
        var myTrades = db.Trades.AsNoTracking().Where(t => t.UserId == userId);

        var total = await myTrades.CountAsync(ct);

        // Newest first. Equal times are ordered by id, so pages never repeat or skip a row.
        var rows = await (
                from t in myTrades
                join i in db.Instruments on t.InstrumentId equals i.Id
                orderby t.ExecutedAt descending, t.Id descending
                select new TradeRow(t.Id, i.Symbol, t.Side, t.Quantity, t.Price, t.ExecutedAt, t.CostBasis))
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = rows.Select(ToResponse).ToList();

        return Results.Ok(new PagedResult<TradeResponse>(items, request.Page, request.PageSize, total));
    }

    private static async Task<IResult> GetTrade(
        string id,
        [FromHeader(Name = "X-User-Id")] string? userIdHeader,
        TradeDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        if (!CurrentUser.TryParse(userIdHeader, out var userId))
        {
            return CurrentUser.Missing(http);
        }

        // Not a valid id, or somebody else's trade: both look exactly like "not found".
        if (!Guid.TryParse(id, out var tradeId))
        {
            return ApiResults.NotFound(http, "Trade not found.");
        }

        var row = await (
                from t in db.Trades.AsNoTracking()
                join i in db.Instruments on t.InstrumentId equals i.Id
                where t.Id == tradeId && t.UserId == userId
                select new TradeRow(t.Id, i.Symbol, t.Side, t.Quantity, t.Price, t.ExecutedAt, t.CostBasis))
            .FirstOrDefaultAsync(ct);

        return row is null
            ? ApiResults.NotFound(http, "Trade not found.")
            : Results.Ok(ToResponse(row));
    }

    private static TradeResponse ToResponse(TradeRow row) => new(
        row.Id,
        row.Symbol,
        row.Side.ToString().ToUpperInvariant(),
        row.Quantity,
        row.Price,
        row.ExecutedAt,
        row.CostBasis,
        row.CostBasis is null ? null : row.Quantity * row.Price - row.CostBasis);
}