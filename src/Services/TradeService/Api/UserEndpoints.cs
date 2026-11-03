using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TradeFolio.Common.Errors;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Api;

public sealed record UserProfileResponse(Guid Id, string Email, DateTimeOffset CreatedAt, string Status);

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/me", GetMe).WithName("GetMe");

        return app;
    }

    private static async Task<IResult> GetMe(
        ClaimsPrincipal principal,
        TradeDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var sub = principal.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId))
        {
            return Unauthorized(http);
        }

        var row = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.Status == UserStatus.Active)
            .Select(u => new { u.Id, u.Email, u.CreatedAt, u.Status })
            .FirstOrDefaultAsync(ct);

        // A valid token for a user that no longer exists or is disabled is not accepted.
        return row is null
            ? Unauthorized(http)
            : Results.Ok(new UserProfileResponse(row.Id, row.Email, row.CreatedAt, row.Status.ToString()));
    }

    private static IResult Unauthorized(HttpContext http) =>
        ApiResults.Problem(http, ApiProblem.Unauthorized(http.TraceIdentifier));
}