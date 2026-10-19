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

app.Run();