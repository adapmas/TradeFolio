using System.Text.Json.Serialization;

namespace TradeFolio.Common.Errors;

public sealed record ApiProblem(
    string Type,
    string Title,
    int Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TraceId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string[]>? Errors = null)
{
    private const string BaseUri = "https://api.tradefolio.local/errors/";

    public static ApiProblem Validation(IReadOnlyDictionary<string, string[]> errors, string? traceId = null) =>
        new(BaseUri + "validation", "Validation failed", 400, traceId, errors);

    public static ApiProblem Unauthorized(string? traceId = null) =>
        new(BaseUri + "unauthorized", "Authentication required", 401, traceId);

    public static ApiProblem NotFound(string title = "Resource not found", string? traceId = null) =>
        new(BaseUri + "not-found", title, 404, traceId);

    public static ApiProblem Conflict(string title, string? traceId = null) =>
        new(BaseUri + "conflict", title, 409, traceId);
}