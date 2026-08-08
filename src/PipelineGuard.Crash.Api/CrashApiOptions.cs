namespace PipelineGuard.Crash.Api;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Uri { get; init; } = "amqp://guest:guest@localhost:5672/";

    public int PublishAttempts { get; init; } = 3;

    public int ConfirmTimeoutSeconds { get; init; } = 5;
}

public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Url { get; init; } = "http://localhost:9200";

    public string CrashIndex { get; init; } = "pipelineguard-crashes-v1";

    public string ResolutionIndex { get; init; } = "pipelineguard-crash-resolutions-v1";
}
