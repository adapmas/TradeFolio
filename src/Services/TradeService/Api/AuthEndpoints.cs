using TradeFolio.Common.Errors;
using TradeService.Application;

namespace TradeService.Api;

public sealed record RegisterResponse(Guid Id, string Email, DateTimeOffset CreatedAt);

public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn, DateTimeOffset ExpiresAt);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", Register)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .WithName("Register");

        group.MapPost("/login", Login)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .WithName("Login");

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

    private static async Task<IResult> Login(
        LoginRequest? request,
        IUserAuthenticator authenticator,
        ITokenService tokens,
        HttpContext http,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await authenticator.AuthenticateAsync(request.Email!, request.Password!, ct);

        if (user is null)
        {
            // One answer for unknown email, wrong password and disabled account.
            return ApiResults.Problem(http, new ApiProblem(
                "https://api.tradefolio.local/errors/invalid-credentials",
                "Invalid email or password",
                StatusCodes.Status401Unauthorized,
                http.TraceIdentifier));
        }

        var token = tokens.CreateAccessToken(user);

        // Tokens must not be cached by browsers or proxies.
        http.Response.Headers.CacheControl = "no-store";

        return Results.Ok(new LoginResponse(token.Value, "Bearer", token.ExpiresInSeconds, token.ExpiresAt));
    }
}