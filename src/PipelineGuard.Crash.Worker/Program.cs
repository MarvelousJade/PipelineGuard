using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PipelineGuard.Crash.Worker;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls("http://127.0.0.1:5081");
}

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<ElasticsearchOptions>(
    builder.Configuration.GetSection(ElasticsearchOptions.SectionName));
builder.Services.AddHttpClient("elasticsearch", (provider, client) =>
{
    ElasticsearchOptions options = provider.GetRequiredService<IOptions<ElasticsearchOptions>>().Value;
    client.BaseAddress = new Uri(options.Url.TrimEnd('/') + '/', UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton(provider => new ElasticsearchCrashIndex(
    provider.GetRequiredService<IHttpClientFactory>().CreateClient("elasticsearch"),
    provider.GetRequiredService<IOptions<ElasticsearchOptions>>()));
builder.Services.AddSingleton<ICrashIndex>(provider => provider.GetRequiredService<ElasticsearchCrashIndex>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CrashEventProcessor>();
builder.Services.AddSingleton<RabbitCrashConsumer>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<RabbitCrashConsumer>());
builder.Services.AddHealthChecks()
    .AddCheck<RabbitConsumerHealthCheck>("rabbitmq", tags: new[] { "ready" })
    .AddCheck<ElasticsearchHealthCheck>("elasticsearch", tags: new[] { "ready" });

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
});
ResourceBuilder resource = ResourceBuilder.CreateDefault().AddService(WorkerTelemetry.ServiceName);
builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.SetResourceBuilder(resource);
    ConfigureLogExporter(options, builder.Configuration);
});
var telemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resources => resources.AddService(WorkerTelemetry.ServiceName));
telemetry.WithTracing(tracing =>
{
    tracing.AddSource(WorkerTelemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation();
    ConfigureTraceExporter(tracing, builder.Configuration);
});
telemetry.WithMetrics(metrics =>
{
    metrics.AddMeter(WorkerTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation();
    ConfigureMetricExporter(metrics, builder.Configuration);
});

var app = builder.Build();
app.MapGet("/", () => Results.Ok(new
{
    service = WorkerTelemetry.ServiceName,
    version = "1.0",
    health = new[] { "/health/live", "/health/ready" }
}));
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponseAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
});
await app.RunAsync().ConfigureAwait(false);

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    context.Response.StatusCode = report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;
    return context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString().ToLowerInvariant(),
        checks = report.Entries.ToDictionary(
            pair => pair.Key,
            pair => new { status = pair.Value.Status.ToString().ToLowerInvariant(), pair.Value.Description })
    });
}

static void ConfigureLogExporter(OpenTelemetryLoggerOptions options, IConfiguration configuration)
{
    if (Uri.TryCreate(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"], UriKind.Absolute, out Uri? endpoint))
    {
        options.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
    }
    else
    {
        options.AddConsoleExporter();
    }
}

static void ConfigureTraceExporter(TracerProviderBuilder tracing, IConfiguration configuration)
{
    if (Uri.TryCreate(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"], UriKind.Absolute, out Uri? endpoint))
    {
        tracing.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
    }
    else
    {
        tracing.AddConsoleExporter();
    }
}

static void ConfigureMetricExporter(MeterProviderBuilder metrics, IConfiguration configuration)
{
    if (Uri.TryCreate(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"], UriKind.Absolute, out Uri? endpoint))
    {
        metrics.AddOtlpExporter(exporter => exporter.Endpoint = endpoint);
    }
    else
    {
        metrics.AddConsoleExporter();
    }
}
