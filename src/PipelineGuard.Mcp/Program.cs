using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PipelineGuard.Mcp;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

string configuredApiUrl = builder.Configuration["PIPELINEGUARD_API_URL"]
    ?? "http://127.0.0.1:5078/";
if (!Uri.TryCreate(configuredApiUrl, UriKind.Absolute, out Uri? apiUri))
{
    throw new InvalidOperationException("PIPELINEGUARD_API_URL must be an absolute URI.");
}

bool allowRemote = string.Equals(
    builder.Configuration["PIPELINEGUARD_ALLOW_REMOTE_API"],
    "true",
    StringComparison.OrdinalIgnoreCase);
if (!apiUri.IsLoopback && !allowRemote)
{
    throw new InvalidOperationException(
        "The MCP server only connects to a loopback API unless PIPELINEGUARD_ALLOW_REMOTE_API=true.");
}

builder.Services.AddHttpClient<PipelineGuardApiClient>(client =>
{
    client.BaseAddress = apiUri;
    client.Timeout = TimeSpan.FromSeconds(10);
});

var telemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(McpTelemetry.ServiceName))
    .WithTracing(tracing => tracing
        .AddSource(McpTelemetry.SourceName)
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics.AddMeter(McpTelemetry.MeterName));

if (Uri.TryCreate(
    builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
    UriKind.Absolute,
    out Uri? otlpEndpoint))
{
    telemetry
        .WithTracing(tracing => tracing.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint))
        .WithMetrics(metrics => metrics.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint));
}

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ValidationTools>();

await builder.Build().RunAsync().ConfigureAwait(false);
