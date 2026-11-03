using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TradeFolio.Common.Errors;

namespace TradeFolio.Gateway;

public static class GatewayRateLimiting
{
    public const string General = "general";
    public const string AuthStrict = "auth-strict";

    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Ordinary API traffic: each signed-in user has their own bucket; anonymous callers share by IP.
            options.AddPolicy(General, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    PartitionKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 60,
                        TokensPerPeriod = 10,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            // Login and registration: small bucket per IP, to slow password guessing and signup spam.
            options.AddPolicy(AuthStrict, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    $"ip:{ClientIp(context)}",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 5,
                        TokensPerPeriod = 1,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(12),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                    http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                }

                var problem = new ApiProblem(
                    "https://api.tradefolio.local/errors/rate-limited",
                    "Too many requests",
                    StatusCodes.Status429TooManyRequests,
                    http.TraceIdentifier);

                await Results.Json(problem, contentType: "application/problem+json", statusCode: problem.Status)
                    .ExecuteAsync(http);
            };
        });

        return services;
    }

    private static string PartitionKey(HttpContext context) =>
        context.User.FindFirst("sub")?.Value is { Length: > 0 } userId
            ? $"user:{userId}"
            : $"ip:{ClientIp(context)}";

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}