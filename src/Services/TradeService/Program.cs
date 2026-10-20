using TradeFolio.Common.Errors;
var builder = WebApplication.CreateBuilder(args);

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