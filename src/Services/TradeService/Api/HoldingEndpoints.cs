using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradeFolio.Common.Pagination;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Api;

public sealed record HoldingResponse(
    string Symbol,
    string Name,
    string Sector,
    string Currency,
    decimal Quantity,
    decimal CostBasis,
    decimal AverageCost,
    int OpenLots,
    DateTimeOffset UpdatedAt);

internal sealed record HoldingRow(
    string Symbol,
    string Name,
    string Sector,
    string Currency,
    decimal Quantity,
    DateTimeOffset UpdatedAt,
    decimal CostBasis,
    int OpenLots);

public static class HoldingEndpoints
{
    public static IEndpointRouteBuilder MapHoldingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/holdings").WithTags("Holdings");

        group.MapGet("/", ListHoldings).WithName("ListHoldings");

        return app;
    }

    private static async Task<IResult> ListHoldings(
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

        // Holdings that were sold down to zero are kept in the table but not listed.
        var open = db.Holdings.AsNoTracking().Where(h => h.UserId == userId && h.Quantity > 0m);

        var total = await open.CountAsync(ct);

        var rows = await (
                from h in open
                join i in db.Instruments on h.InstrumentId equals i.Id
                orderby i.Symbol
                select new HoldingRow(
                    i.Symbol,
                    i.Name,
                    i.Sector,
                    i.Currency,
                    h.Quantity,
                    h.UpdatedAt,
                    db.HoldingLots
                        .Where(l => l.HoldingId == h.Id && l.RemainingQuantity > 0m)
                        .Sum(l => (decimal?)(l.RemainingQuantity * l.PurchasePrice)) ?? 0m,
                    db.HoldingLots
                        .Count(l => l.HoldingId == h.Id && l.RemainingQuantity > 0m)))
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = rows
            .Select(r => new HoldingResponse(
                r.Symbol,
                r.Name,
                r.Sector,
                r.Currency,
                r.Quantity,
                r.CostBasis,
                r.Quantity > 0m ? r.CostBasis / r.Quantity : 0m,
                r.OpenLots,
                r.UpdatedAt))
            .ToList();

        return Results.Ok(new PagedResult<HoldingResponse>(items, request.Page, request.PageSize, total));
    }
}