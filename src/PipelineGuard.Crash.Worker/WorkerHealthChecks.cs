using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PipelineGuard.Crash.Worker;

public sealed class RabbitConsumerHealthCheck : IHealthCheck
{
    private readonly RabbitCrashConsumer _consumer;

    public RabbitConsumerHealthCheck(RabbitCrashConsumer consumer) => _consumer = consumer;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_consumer.IsReady
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("RabbitMQ consumer is disconnected."));
}

public sealed class ElasticsearchHealthCheck : IHealthCheck
{
    private readonly ElasticsearchCrashIndex _index;

    public ElasticsearchHealthCheck(ElasticsearchCrashIndex index) => _index = index;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _index.IsHealthyAsync(cancellationToken).ConfigureAwait(false)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Elasticsearch returned an unhealthy response.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Elasticsearch is unavailable.", exception);
        }
    }
}
