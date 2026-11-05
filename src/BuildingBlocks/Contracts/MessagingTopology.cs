namespace TradeFolio.Contracts;

/// <summary>
/// Names of the RabbitMQ exchanges, queues and routing keys. They must match
/// infrastructure/docker/rabbitmq/definitions.json (a unit test checks that they do).
/// </summary>
public static class MessagingTopology
{
    public const string Exchange = "tradefolio.events";
    public const string DeadLetterExchange = "tradefolio.events.dlx";

    public const string TradeCompletedRoutingKey = "trade.completed";

    public const string NotificationQueue = "notification.trade-completed";
    public const string NotificationDeadLetterQueue = "notification.trade-completed.dlq";
}
