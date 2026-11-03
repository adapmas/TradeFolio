namespace TradeFolio.Gateway;

public static class GatewayCors
{
    public const string PolicyName = "frontend";

    public static IServiceCollection AddGatewayCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        ValidateOrigins(origins);

        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST")
            .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "X-Correlation-Id")
            .WithExposedHeaders("X-Correlation-Id", "Idempotency-Replayed", "Retry-After")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

        return services;
    }

    /// <summary>Fails at startup on an empty list, a wildcard, or anything that is not a plain origin.</summary>
    public static void ValidateOrigins(IReadOnlyCollection<string> origins)
    {
        if (origins.Count == 0)
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must list at least one origin (for example http://localhost:5173).");
        }

        foreach (var origin in origins)
        {
            var isPlainOrigin =
                !origin.Contains('*') &&
                !origin.EndsWith('/') &&
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                uri.AbsolutePath == "/" &&
                uri.Query.Length == 0 &&
                uri.Fragment.Length == 0;

            if (!isPlainOrigin)
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins contains '{origin}'. Use exact origins like http://localhost:5173, " +
                    "with no wildcard, path or trailing slash.");
            }
        }
    }
}