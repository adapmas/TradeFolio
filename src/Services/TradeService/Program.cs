using TradeFolio.Common.Security;
using Microsoft.EntityFrameworkCore;
using TradeFolio.Common.Errors;
using TradeService.Infrastructure.Persistence;
using TradeService.Api;
using FluentValidation;
using TradeService.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using TradeFolio.Common.Correlation;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TradeDb")
    ?? throw new InvalidOperationException(
        "Connection string 'TradeDb' is missing. Set the ConnectionStrings__TradeDb environment variable.");

builder.Services.AddDbContext<TradeDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IInstrumentLookup, InstrumentLookup>();
builder.Services.AddScoped<IValidator<CreateTradeRequest>, CreateTradeRequestValidator>();
builder.Services.AddScoped<ITradeExecutor, TradeExecutor>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.Validate();   // fails at startup if the signing key is missing or weak
builder.Services.Configure<JwtOptions>(jwtSection);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IUserAuthenticator, UserAuthenticator>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep "sub" as "sub"
        options.TokenValidationParameters = jwtOptions.CreateValidationParameters();
        options.Events = new JwtBearerEvents
        {
            // Missing, expired or tampered token: answer with the standard error shape.
            OnChallenge = context =>
            {
                context.HandleResponse();
                var http = context.HttpContext;
                return ApiResults.Problem(http, ApiProblem.Unauthorized(http.TraceIdentifier)).ExecuteAsync(http);
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IUserRegistrar, UserRegistrar>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the accessToken from POST /api/v1/auth/login."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddHealthChecks();

var app = builder.Build();

// Every request gets a correlation id: the caller's (if it is a valid GUID) or a new one.
// It becomes the trace id in error responses and is echoed back in a response header.
app.Use(async (context, next) =>
{
    var correlationId = CorrelationId.Resolve(context.Request.Headers[CorrelationId.HeaderName].ToString());
    context.TraceIdentifier = correlationId;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers[CorrelationId.HeaderName] = correlationId;
        return Task.CompletedTask;
    });
    await next();
});

var serviceName = app.Configuration["Service:Name"] ?? "trade-service";
app.Logger.LogInformation("Starting {ServiceName}", serviceName);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapInstrumentEndpoints();
app.MapTradeEndpoints();
app.MapHoldingEndpoints();
app.MapAuthEndpoints();
app.MapUserEndpoints();

app.MapGet("/api/v1/ping", () => Results.Ok(new { service = serviceName, status = "ok" }))
   .WithName("Ping");

app.MapGet("/api/v1/ping/error", (HttpContext http) =>
{
    var problem = ApiProblem.Validation(
        new Dictionary<string, string[]> { ["quantity"] = ["Quantity must be greater than zero."] },
        http.TraceIdentifier);
    return Results.Json(problem, contentType: "application/problem+json", statusCode: problem.Status);
}).WithName("PingError");

app.Run();
public partial class Program { }
