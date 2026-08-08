using RabbitMQ.Client;

namespace PipelineGuard.Crash.Api;

internal static class RabbitMqTopology
{
    public const string Exchange = "pipelineguard.crashes";
    public const string RoutingKey = "crash.report.v1";
    public const string Queue = "pipelineguard.crash.processing";
    public const string DeadLetterExchange = "pipelineguard.crashes.dead-letter";
    public const string DeadLetterRoutingKey = "crash.dead.v1";
    public const string DeadLetterQueue = "pipelineguard.crash.dead-letter";

    public static void Declare(IModel channel)
    {
        channel.ExchangeDeclare(Exchange, ExchangeType.Direct, durable: true, autoDelete: false);
        channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false);

        var queueArguments = new Dictionary<string, object>
        {
            ["x-queue-type"] = "quorum",
            ["x-delivery-limit"] = 5,
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DeadLetterRoutingKey
        };
        channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false, queueArguments);
        channel.QueueBind(Queue, Exchange, RoutingKey);

        var deadLetterArguments = new Dictionary<string, object>
        {
            ["x-queue-type"] = "quorum"
        };
        channel.QueueDeclare(
            DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            deadLetterArguments);
        channel.QueueBind(DeadLetterQueue, DeadLetterExchange, DeadLetterRoutingKey);
    }
}
