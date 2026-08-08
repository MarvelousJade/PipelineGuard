using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PipelineGuard.Crash.Api;

public sealed class RabbitPublisherHealthCheck : IHealthCheck
{
    private readonly RabbitCrashPublisher _publisher;

    public RabbitPublisherHealthCheck(RabbitCrashPublisher publisher) => _publisher = publisher;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        await _publisher.EnsureReadyAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("RabbitMQ publisher is disconnected.");
}

public sealed class ElasticsearchHealthCheck : IHealthCheck
{
    private readonly CrashSearchClient _client;

    public ElasticsearchHealthCheck(CrashSearchClient client) => _client = client;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.IsHealthyAsync(cancellationToken).ConfigureAwait(false)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Elasticsearch returned an unhealthy response.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Elasticsearch is unavailable.", exception);
        }
    }
}
