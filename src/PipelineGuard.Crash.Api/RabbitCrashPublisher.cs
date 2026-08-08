using System.Text.Json;
using Microsoft.Extensions.Options;
using PipelineGuard.CrashIntelligence;
using RabbitMQ.Client;

namespace PipelineGuard.Crash.Api;

public sealed class RabbitCrashPublisher : IHostedService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitCrashPublisher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitCrashPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitCrashPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsReady => _connection?.IsOpen == true && _channel?.IsOpen == true;

    public async Task<bool> EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (IsReady)
        {
            return true;
        }

        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            return IsReady;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogDebug(exception, "RabbitMQ readiness reconnect attempt failed");
            return false;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "RabbitMQ is unavailable at startup; readiness will remain unhealthy");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        DisposeConnection();
        return Task.CompletedTask;
    }

    public async Task PublishAsync(CrashEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
        int attempts = Math.Clamp(_options.PublishAttempts, 1, 5);

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureConnected();
                IBasicProperties properties = _channel!.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";
                properties.Type = "pipelineguard.crash.report.v1";
                properties.MessageId = envelope.MessageId.ToString("D");
                properties.Timestamp = new AmqpTimestamp(envelope.ReceivedAtUtc.ToUnixTimeSeconds());

                _channel.BasicPublish(
                    RabbitMqTopology.Exchange,
                    RabbitMqTopology.RoutingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: body);
                _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(
                    Math.Clamp(_options.ConfirmTimeoutSeconds, 1, 30)));
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                DisposeConnection();
                _logger.LogWarning(
                    exception,
                    "RabbitMQ publish attempt {Attempt} of {Attempts} failed",
                    attempt,
                    attempts);
                if (attempt == attempts)
                {
                    throw new CrashBrokerUnavailableException("RabbitMQ did not confirm the crash event.", exception);
                }
            }
            finally
            {
                _gate.Release();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureConnected()
    {
        if (IsReady)
        {
            return;
        }

        DisposeConnection();
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.Uri, UriKind.Absolute),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            RequestedHeartbeat = TimeSpan.FromSeconds(15),
            ClientProvidedName = "pipelineguard-crash-api"
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        RabbitMqTopology.Declare(_channel);
        _channel.ConfirmSelect();
    }

    private void DisposeConnection()
    {
        try
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Error while closing RabbitMQ publisher resources");
        }
        finally
        {
            _channel = null;
            _connection = null;
        }
    }

    public void Dispose()
    {
        DisposeConnection();
        _gate.Dispose();
    }
}

public sealed class CrashBrokerUnavailableException : Exception
{
    public CrashBrokerUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
