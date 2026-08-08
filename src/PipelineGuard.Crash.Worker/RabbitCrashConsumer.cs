using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PipelineGuard.CrashIntelligence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PipelineGuard.Crash.Worker;

public sealed class RabbitCrashConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly RabbitMqOptions _options;
    private readonly ElasticsearchCrashIndex _index;
    private readonly CrashEventProcessor _processor;
    private readonly ILogger<RabbitCrashConsumer> _logger;
    private volatile bool _isReady;

    public RabbitCrashConsumer(
        IOptions<RabbitMqOptions> options,
        ElasticsearchCrashIndex index,
        CrashEventProcessor processor,
        ILogger<RabbitCrashConsumer> logger)
    {
        _options = options.Value;
        _index = index;
        _processor = processor;
        _logger = logger;
    }

    public bool IsReady => _isReady;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConsumerAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _isReady = false;
                _logger.LogError(exception, "Crash consumer disconnected; reconnecting in two seconds");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunConsumerAsync(CancellationToken stoppingToken)
    {
        await _index.EnsureIndexAsync(stoppingToken).ConfigureAwait(false);

        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.Uri, UriKind.Absolute),
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = false,
            RequestedHeartbeat = TimeSpan.FromSeconds(15),
            ClientProvidedName = "pipelineguard-crash-worker"
        };
        using IConnection connection = factory.CreateConnection();
        using IModel channel = connection.CreateModel();
        RabbitMqTopology.Declare(channel);
        channel.BasicQos(0, Math.Clamp(_options.PrefetchCount, (ushort)1, (ushort)128), global: false);

        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdown += (_, _) => disconnected.TrySetResult();
        connection.CallbackException += (_, args) => disconnected.TrySetException(args.Exception);
        using CancellationTokenRegistration registration = stoppingToken.Register(
            () => disconnected.TrySetResult());

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += (_, args) => HandleDeliveryAsync(channel, args, stoppingToken);
        channel.BasicConsume(RabbitMqTopology.Queue, autoAck: false, consumer);
        _isReady = true;
        _logger.LogInformation(
            "Consuming {Queue} with prefetch {Prefetch}; poison deliveries dead-letter after five attempts",
            RabbitMqTopology.Queue,
            _options.PrefetchCount);

        try
        {
            await disconnected.Task.ConfigureAwait(false);
        }
        finally
        {
            _isReady = false;
        }
    }

    private async Task HandleDeliveryAsync(
        IModel channel,
        BasicDeliverEventArgs args,
        CancellationToken stoppingToken)
    {
        Guid messageId = Guid.Empty;
        try
        {
            CrashEnvelope envelope = JsonSerializer.Deserialize<CrashEnvelope>(args.Body.Span, SerializerOptions)
                ?? throw new JsonException("The crash event body was empty.");
            messageId = envelope.MessageId;
            ValidateEnvelope(envelope);
            CrashIndexWriteOutcome outcome = await _processor.ProcessAsync(envelope, stoppingToken)
                .ConfigureAwait(false);
            channel.BasicAck(args.DeliveryTag, multiple: false);
            _logger.LogInformation(
                "Crash message {MessageId} completed with outcome {Outcome}",
                messageId,
                outcome);
        }
        catch (JsonException exception)
        {
            RejectPoisonDelivery(channel, args.DeliveryTag, messageId, "invalid_json", exception);
        }
        catch (CrashReportValidationException exception)
        {
            RejectPoisonDelivery(channel, args.DeliveryTag, messageId, "invalid_contract", exception);
        }
        catch (CrashIndexPermanentException exception)
        {
            RejectPoisonDelivery(channel, args.DeliveryTag, messageId, "permanent_index_error", exception);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            SafeNack(channel, args.DeliveryTag, requeue: true);
        }
        catch (Exception exception)
        {
            WorkerTelemetry.RecordDeliveryFailure("transient");
            int deliveryCount = GetDeliveryCount(args.BasicProperties.Headers);
            TimeSpan retryDelay = TimeSpan.FromMilliseconds(
                Math.Min(500 * Math.Pow(2, deliveryCount), 8_000));
            _logger.LogWarning(
                exception,
                "Transient processing failure for message {MessageId}; delivery {DeliveryCount} will retry after {RetryDelayMs} ms",
                messageId,
                deliveryCount + 1,
                retryDelay.TotalMilliseconds);
            try
            {
                await Task.Delay(retryDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                SafeNack(channel, args.DeliveryTag, requeue: true);
                return;
            }

            SafeNack(channel, args.DeliveryTag, requeue: true);
        }
    }

    private static void ValidateEnvelope(CrashEnvelope envelope)
    {
        var errors = new List<string>();
        if (!string.Equals(envelope.EventVersion, "1.0", StringComparison.Ordinal))
        {
            errors.Add("eventVersion must be '1.0'.");
        }

        if (envelope.MessageId == Guid.Empty || envelope.MessageId != envelope.Report.CrashId)
        {
            errors.Add("messageId must be non-empty and match report.crashId.");
        }

        CrashReportValidator.Validate(envelope.Report);
        if (errors.Count > 0)
        {
            throw new CrashReportValidationException(errors);
        }
    }

    private void RejectPoisonDelivery(
        IModel channel,
        ulong deliveryTag,
        Guid messageId,
        string reason,
        Exception exception)
    {
        WorkerTelemetry.RecordDeliveryFailure(reason);
        _logger.LogError(
            exception,
            "Rejecting poison crash message {MessageId} with reason {Reason}",
            messageId,
            reason);
        SafeNack(channel, deliveryTag, requeue: false);
    }

    private static int GetDeliveryCount(IDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-delivery-count", out object? value))
        {
            return 0;
        }

        return value switch
        {
            byte count => count,
            short count => count,
            int count => count,
            long count when count <= int.MaxValue => (int)count,
            _ => 0
        };
    }

    private static void SafeNack(IModel channel, ulong deliveryTag, bool requeue)
    {
        if (channel.IsOpen)
        {
            channel.BasicNack(deliveryTag, multiple: false, requeue);
        }
    }
}
