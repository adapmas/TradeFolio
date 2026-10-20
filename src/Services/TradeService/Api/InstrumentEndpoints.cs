using Microsoft.EntityFrameworkCore;
using TradeFolio.Common.Errors;
using TradeFolio.Common.Pagination;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Api;

public sealed record InstrumentResponse(Guid Id, string Symbol, string Name, string Sector, string Currency);

public static class InstrumentEndpoints
{
    public static IEndpointRouteBuilder MapInstrumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/instruments").WithTags("Instruments");

        group.MapGet("/", async (int? page, int? pageSize, TradeDbContext db, CancellationToken ct) =>
        {
            var request = PageRequest.Create(page, pageSize);

            var total = await db.Instruments.CountAsync(ct);
            var items = await db.Instruments
                .AsNoTracking()
                .OrderBy(i => i.Symbol)
                .Skip(request.Skip)
                .Take(request.PageSize)
                .Select(i => new InstrumentResponse(i.Id, i.Symbol, i.Name, i.Sector, i.Currency))
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<InstrumentResponse>(items, request.Page, request.PageSize, total));
        }).WithName("ListInstruments");

        group.MapGet("/{symbol}", async (string symbol, TradeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var normalized = symbol.Trim().ToUpperInvariant();

            var instrument = await db.Instruments
                .AsNoTracking()
                .Where(i => i.Symbol == normalized)
                .Select(i => new InstrumentResponse(i.Id, i.Symbol, i.Name, i.Sector, i.Currency))
                .FirstOrDefaultAsync(ct);

            if (instrument is null)
            {
                var problem = ApiProblem.NotFound($"Instrument '{normalized}' was not found.", http.TraceIdentifier);
                return Results.Json(problem, contentType: "application/problem+json", statusCode: problem.Status);
            }

            return Results.Ok(instrument);
        }).WithName("GetInstrumentBySymbol");

        return app;
    }
}