using System.Text.Json;
using TradeFolio.Common.Errors;

namespace Common.UnitTests;

public class ApiProblemTests
{
    [Fact]
    public void Validation_sets_status_400_and_keeps_errors()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["quantity"] = ["Quantity must be greater than zero."]
        };

        var problem = ApiProblem.Validation(errors, "trace-1");

        Assert.Equal(400, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
        Assert.Equal("https://api.tradefolio.local/errors/validation", problem.Type);
        Assert.Equal("trace-1", problem.TraceId);
        Assert.Equal("Quantity must be greater than zero.", problem.Errors!["quantity"][0]);
    }

    [Fact]
    public void Serialized_json_matches_the_spec_shape()
    {
        var problem = ApiProblem.Validation(
            new Dictionary<string, string[]> { ["quantity"] = ["Quantity must be greater than zero."] },
            "01J");

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("https://api.tradefolio.local/errors/validation", root.GetProperty("type").GetString());
        Assert.Equal("Validation failed", root.GetProperty("title").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("01J", root.GetProperty("traceId").GetString());
        Assert.Equal("Quantity must be greater than zero.",
            root.GetProperty("errors").GetProperty("quantity")[0].GetString());
    }

    [Fact]
    public void Empty_trace_id_and_errors_are_left_out_of_the_json()
    {
        var json = JsonSerializer.Serialize(ApiProblem.NotFound(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("traceId", out _));
        Assert.False(doc.RootElement.TryGetProperty("errors", out _));
        Assert.Equal(404, doc.RootElement.GetProperty("status").GetInt32());
    }
}