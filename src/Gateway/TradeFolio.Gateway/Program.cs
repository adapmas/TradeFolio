using Microsoft.AspNetCore.Authentication.JwtBearer;
using TradeFolio.Common.Correlation;
using TradeFolio.Common.Errors;
using TradeFolio.Common.Security;
using TradeFolio.Gateway;

var builder = WebApplication.CreateBuilder(args);

// Request limits: every request here is a small JSON body, and the server product name is not advertised.
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 64 * 1024;
});
// Same rules as the services: the gateway rejects bad tokens early, the services check again.
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.Validate();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwtOptions.CreateValidationParameters();
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                var http = context.HttpContext;
                var problem = ApiProblem.Unauthorized(http.TraceIdentifier);
                return Results.Json(problem, contentType: "application/problem+json", statusCode: problem.Status)
                    .ExecuteAsync(http);
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddGatewayCors(builder.Configuration);
builder.Services.AddGatewayRateLimiting();
builder.Services.AddHealthChecks();
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Correlation id: keep the caller's if it is a GUID, otherwise create one. It is passed on to the
// service and returned in the response, so one id follows a request through the whole system.
app.Use(async (context, next) =>
{
    var correlationId = CorrelationId.Resolve(context.Request.Headers[CorrelationId.HeaderName].ToString());
    context.TraceIdentifier = correlationId;
    context.Request.Headers[CorrelationId.HeaderName] = correlationId;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers[CorrelationId.HeaderName] = correlationId;
        return Task.CompletedTask;
    });

    using (app.Logger.BeginScope(new Dictionary<string, object> { ["correlation_id"] = correlationId }))
    {
        await next();
    }
});

// Last-resort handler: any unexpected exception becomes a safe 500 with only the trace id.
// (The middleware logs the exception itself, with the correlation id in scope.)
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var problem = new ApiProblem(
        "https://api.tradefolio.local/errors/internal",
        "An unexpected error occurred",
        StatusCodes.Status500InternalServerError,
        context.TraceIdentifier);

    await Results.Json(problem, contentType: "application/problem+json", statusCode: problem.Status)
        .ExecuteAsync(context);
}));

// Basic hardening headers on every response (an API never needs to be framed or sniffed).
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        return Task.CompletedTask;
    });
    await next();
});

// Order matters:
//  CORS first, so browser preflight requests are answered before authentication can refuse them;
//  authentication before the limiter, so signed-in users get their own bucket;
//  the limiter before authorization, so floods of token-less requests are limited too.
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();

public partial class Program { }