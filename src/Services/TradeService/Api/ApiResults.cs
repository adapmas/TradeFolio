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
/// TEMPORARY: the user id comes from the X-User-Id header until login exists.
/// In Phase 5 this one class changes to read the id from the JWT instead.
/// </summary>
public static class CurrentUser
{
    public static bool TryParse(string? header, out Guid userId) => Guid.TryParse(header, out userId);

    public static IResult Missing(HttpContext http) =>
        ApiResults.Problem(http, ApiProblem.Validation(
            new Dictionary<string, string[]>
            {
                ["X-User-Id"] = ["A valid X-User-Id header (a GUID) is required. This is temporary until login exists."]
            },
            http.TraceIdentifier));
}