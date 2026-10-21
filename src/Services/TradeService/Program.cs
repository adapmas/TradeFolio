using Microsoft.EntityFrameworkCore;
using TradeFolio.Common.Errors;
using TradeService.Infrastructure.Persistence;
using TradeService.Api;
using FluentValidation;
using TradeService.Application;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TradeDb")
    ?? throw new InvalidOperationException(
        "Connection string 'TradeDb' is missing. Set the ConnectionStrings__TradeDb environment variable.");

builder.Services.AddDbContext<TradeDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IInstrumentLookup, InstrumentLookup>();
builder.Services.AddScoped<IValidator<CreateTradeRequest>, CreateTradeRequestValidator>();
builder.Services.AddScoped<ITradeExecutor, TradeExecutor>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

var serviceName = app.Configuration["Service:Name"] ?? "trade-service";
app.Logger.LogInformation("Starting {ServiceName}", serviceName);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapInstrumentEndpoints();
app.MapTradeEndpoints();
app.MapHoldingEndpoints();

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