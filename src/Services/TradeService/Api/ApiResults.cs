using System.Security.Claims;
using TradeFolio.Common.Errors;

namespace TradeService.Api;

public static class ApiResults
{
    public static IResult Problem(HttpContext http, ApiProblem problem) =>
        Results.Json(problem, contentType: "application/problem+json", statusCode: problem.Status);

    public static IResult NotFound(HttpContext http, string title) =>
        Problem(http, ApiProblem.NotFound(title, http.TraceIdentifier));
}

/// <summary>
/// The current user, taken from the validated JWT. This is the only place that decides who
/// the caller is; endpoints never read a user id from a header or a request body.
/// </summary>
public static class CurrentUser
{
    public static bool TryGetId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId);

    public static IResult Unauthorized(HttpContext http) =>
        ApiResults.Problem(http, ApiProblem.Unauthorized(http.TraceIdentifier));
}