namespace PipelineGuard.Crash.Worker;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Uri { get; init; } = "amqp://guest:guest@localhost:5672/";

    public ushort PrefetchCount { get; init; } = 16;
}

public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Url { get; init; } = "http://localhost:9200";

    public string CrashIndex { get; init; } = "pipelineguard-crashes-v1";
}
