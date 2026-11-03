using TradeFolio.Common.Errors;
using TradeService.Application;

namespace TradeService.Api;

public sealed record RegisterResponse(Guid Id, string Email, DateTimeOffset CreatedAt);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", Register)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .WithName("Register");

        return app;
    }

    private static async Task<IResult> Register(
        RegisterRequest? request,
        IUserRegistrar registrar,
        HttpContext http,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request); // the validation filter already rejected a missing body

        var user = await registrar.RegisterAsync(request.Email!, request.Password!, ct);

        if (user is null)
        {
            return ApiResults.Problem(http, ApiProblem.Conflict(
                "An account with this email already exists.", http.TraceIdentifier));
        }

        // Never return the hash.
        var response = new RegisterResponse(user.Id, user.Email, user.CreatedAt);
        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }
}