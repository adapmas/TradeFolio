using FluentValidation;
using TradeFolio.Common.Errors;

namespace TradeService.Api;

public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var request = context.Arguments.OfType<T>().FirstOrDefault();

        if (request is null)
        {
            return ValidationProblem(http, new Dictionary<string, string[]>
            {
                ["body"] = ["A request body is required."]
            });
        }

        var validator = http.RequestServices.GetRequiredService<IValidator<T>>();
        var result = await validator.ValidateAsync(request, http.RequestAborted);

        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(e => ToCamelCase(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

            return ValidationProblem(http, errors);
        }

        return await next(context);
    }

    private static IResult ValidationProblem(HttpContext http, IReadOnlyDictionary<string, string[]> errors)
    {
        var problem = ApiProblem.Validation(errors, http.TraceIdentifier);
        return Results.Json(problem, contentType: "application/problem+json", statusCode: problem.Status);
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}