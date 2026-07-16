using System.Diagnostics;
using System.Text;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PipelineGuard.Api;
using PipelineGuard.Contracts;
using PipelineGuard.Core;

const int MaxReportBytes = 1_048_576;
const int MaxPageSize = 100;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls("http://127.0.0.1:5078");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxReportBytes;
});

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
});

Uri? otlpEndpoint = Uri.TryCreate(
    builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
    UriKind.Absolute,
    out Uri? configuredOtlp)
        ? configuredOtlp
        : null;

ResourceBuilder resource = ResourceBuilder.CreateDefault()
    .AddService(PipelineGuardTelemetry.ServiceName);

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.SetResourceBuilder(resource);
    if (otlpEndpoint is null)
    {
        options.AddConsoleExporter();
    }
    else
    {
        options.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint);
    }
});

var telemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resourceBuilder => resourceBuilder.AddService(PipelineGuardTelemetry.ServiceName));

telemetry.WithTracing(tracing =>
{
    tracing
        .AddSource(PipelineGuardTelemetry.SourceName)
        .AddAspNetCoreInstrumentation();
    if (otlpEndpoint is null)
    {
        tracing.AddConsoleExporter();
    }
    else
    {
        tracing.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint);
    }
});

telemetry.WithMetrics(metrics =>
{
    metrics
        .AddMeter(PipelineGuardTelemetry.MeterName)
        .AddAspNetCoreInstrumentation();
    if (otlpEndpoint is null)
    {
        metrics.AddConsoleExporter();
    }
    else
    {
        metrics.AddOtlpExporter(exporter => exporter.Endpoint = otlpEndpoint);
    }
});

builder.Services.AddSingleton<SqliteReportStore>();

var app = builder.Build();
var store = app.Services.GetRequiredService<SqliteReportStore>();
await store.InitializeAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);

app.MapGet("/", () => Results.Ok(new
{
    service = PipelineGuardTelemetry.ServiceName,
    version = "1.0",
    endpoints = new[] { "/health", "/api/v1/runs", "/openapi/v1.json" }
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = PipelineGuardTelemetry.ServiceName,
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/v1/runs", async (
    int? limit,
    int? offset,
    SqliteReportStore reportStore,
    CancellationToken cancellationToken) =>
{
    int safeLimit = Math.Clamp(limit ?? 50, 1, MaxPageSize);
    int safeOffset = Math.Clamp(offset ?? 0, 0, 100_000);
    IReadOnlyList<StoredRunSummary> runs = await reportStore.ListAsync(
        safeLimit,
        safeOffset,
        cancellationToken).ConfigureAwait(false);
    return Results.Ok(new { offset = safeOffset, limit = safeLimit, items = runs });
});

app.MapGet("/api/v1/runs/{runId:guid}", async (
    Guid runId,
    SqliteReportStore reportStore,
    CancellationToken cancellationToken) =>
{
    string? json = await reportStore.GetReportJsonAsync(runId, cancellationToken)
        .ConfigureAwait(false);
    return json is null
        ? Results.NotFound(new { error = "run_not_found" })
        : Results.Content(json, "application/json", Encoding.UTF8);
});

app.MapPost("/api/v1/runs", async (
    HttpRequest request,
    SqliteReportStore reportStore,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var stopwatch = Stopwatch.StartNew();
    using var activity = PipelineGuardTelemetry.ActivitySource.StartActivity("report.ingest");
    ILogger logger = loggerFactory.CreateLogger("PipelineGuard.ReportIngestion");

    try
    {
        string json = await ReadBodyWithLimitAsync(
            request.Body,
            MaxReportBytes,
            cancellationToken).ConfigureAwait(false);
        ValidationReport report = JsonReportSerializer.Deserialize(json);
        bool inserted = await reportStore.TryAddAsync(report, json, cancellationToken)
            .ConfigureAwait(false);

        if (!inserted)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "duplicate_run_id");
            PipelineGuardTelemetry.RecordRejectedRun("duplicate", stopwatch.Elapsed.TotalMilliseconds);
            return Results.Conflict(new { error = "duplicate_run_id", runId = report.RunId });
        }

        activity?.SetTag("pipelineguard.findings.errors", report.Summary.Errors);
        activity?.SetTag("pipelineguard.findings.warnings", report.Summary.Warnings);
        PipelineGuardTelemetry.RecordAcceptedRun(report, stopwatch.Elapsed.TotalMilliseconds);
        logger.LogInformation(
            "Accepted validation run with {AssetsScanned} assets, {Errors} errors, and {Warnings} warnings",
            report.Summary.AssetsScanned,
            report.Summary.Errors,
            report.Summary.Warnings);

        return Results.Created($"/api/v1/runs/{report.RunId:D}", new { runId = report.RunId });
    }
    catch (ReportTooLargeException)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "report_too_large");
        PipelineGuardTelemetry.RecordRejectedRun("too_large", stopwatch.Elapsed.TotalMilliseconds);
        return Results.Problem(
            statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "Report exceeds the one-megabyte limit.");
    }
    catch (ValidationReportValidationException exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "invalid_report");
        PipelineGuardTelemetry.RecordRejectedRun("invalid", stopwatch.Elapsed.TotalMilliseconds);
        logger.LogWarning(exception, "Rejected an invalid validation report");
        return Results.BadRequest(new { error = "invalid_report", details = exception.Message });
    }
    catch (System.Text.Json.JsonException exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "invalid_json");
        PipelineGuardTelemetry.RecordRejectedRun("invalid_json", stopwatch.Elapsed.TotalMilliseconds);
        logger.LogWarning(exception, "Rejected malformed JSON");
        return Results.BadRequest(new { error = "invalid_json", details = exception.Message });
    }
});

app.MapGet("/openapi/v1.json", () => Results.Ok(new
{
    openapi = "3.1.0",
    info = new { title = "PipelineGuard API", version = "1.0.0" },
    paths = new Dictionary<string, object>
    {
        ["/health"] = new { get = new { summary = "Service health" } },
        ["/api/v1/runs"] = new
        {
            get = new { summary = "List validation runs" },
            post = new { summary = "Ingest a strict v1 validation report" }
        },
        ["/api/v1/runs/{runId}"] = new { get = new { summary = "Get one validation report" } }
    }
}));

await app.RunAsync().ConfigureAwait(false);

static async Task<string> ReadBodyWithLimitAsync(
    Stream body,
    int maxBytes,
    CancellationToken cancellationToken)
{
    await using var buffer = new MemoryStream();
    var chunk = new byte[16 * 1024];
    while (true)
    {
        int read = await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            break;
        }

        if (buffer.Length + read > maxBytes)
        {
            throw new ReportTooLargeException();
        }

        await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
    }

    return Encoding.UTF8.GetString(buffer.ToArray());
}

internal sealed class ReportTooLargeException : Exception
{
}
