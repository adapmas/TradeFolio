using System.Text.Json;
using TradeFolio.Contracts;
using Xunit;

namespace TradeService.UnitTests;

public class RabbitTopologyTests
{
    private static JsonElement LoadDefinitions()
    {
        // Walk up from the test output folder to the repository root (where the solution file is).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("TradeFolio.slnx").Length == 0)
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var path = Path.Combine(dir.FullName, "infrastructure", "docker", "rabbitmq", "definitions.json");
        Assert.True(File.Exists(path), $"Missing {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    private static JsonElement Find(JsonElement root, string section, string name) =>
        root.GetProperty(section).EnumerateArray().Single(e => e.GetProperty("name").GetString() == name);

    private static bool HasBinding(JsonElement root, string source, string destination, string routingKey) =>
        root.GetProperty("bindings").EnumerateArray().Any(b =>
            b.GetProperty("source").GetString() == source &&
            b.GetProperty("destination").GetString() == destination &&
            b.GetProperty("destination_type").GetString() == "queue" &&
            b.GetProperty("routing_key").GetString() == routingKey);

    [Fact]
    public void The_exchanges_are_durable_and_of_the_expected_type()
    {
        var root = LoadDefinitions();

        var events = Find(root, "exchanges", MessagingTopology.Exchange);
        var dlx = Find(root, "exchanges", MessagingTopology.DeadLetterExchange);

        Assert.Equal("topic", events.GetProperty("type").GetString());
        Assert.True(events.GetProperty("durable").GetBoolean());
        Assert.Equal("direct", dlx.GetProperty("type").GetString());
        Assert.True(dlx.GetProperty("durable").GetBoolean());
    }

    [Fact]
    public void The_notification_queue_is_durable_and_dead_letters_into_the_dlx()
    {
        var root = LoadDefinitions();

        var queue = Find(root, "queues", MessagingTopology.NotificationQueue);

        Assert.True(queue.GetProperty("durable").GetBoolean());
        Assert.Equal(
            MessagingTopology.DeadLetterExchange,
            queue.GetProperty("arguments").GetProperty("x-dead-letter-exchange").GetString());
    }

    [Fact]
    public void The_dead_letter_queue_is_durable_and_does_not_dead_letter_again()
    {
        var root = LoadDefinitions();

        var dlq = Find(root, "queues", MessagingTopology.NotificationDeadLetterQueue);

        Assert.True(dlq.GetProperty("durable").GetBoolean());
        Assert.False(dlq.GetProperty("arguments").TryGetProperty("x-dead-letter-exchange", out _));
    }

    [Fact]
    public void Trade_completed_is_routed_to_the_queue_and_dead_letters_to_the_dlq()
    {
        var root = LoadDefinitions();

        Assert.True(HasBinding(
            root,
            MessagingTopology.Exchange,
            MessagingTopology.NotificationQueue,
            MessagingTopology.TradeCompletedRoutingKey));

        Assert.True(HasBinding(
            root,
            MessagingTopology.DeadLetterExchange,
            MessagingTopology.NotificationDeadLetterQueue,
            MessagingTopology.TradeCompletedRoutingKey));
    }
}