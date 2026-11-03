using Microsoft.AspNetCore.Authentication.JwtBearer;
using TradeFolio.Common.Correlation;
using TradeFolio.Common.Errors;
using TradeFolio.Common.Security;

var builder = WebApplication.CreateBuilder(args);

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

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();

public partial class Program { }