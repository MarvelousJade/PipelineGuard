using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PipelineGuard.Crash.Api;
using PipelineGuard.CrashIntelligence;

const int MaxCrashReportBytes = 256 * 1024;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls("http://127.0.0.1:5080");
}

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxCrashReportBytes);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PipelineGuard Crash Intelligence API",
        Version = "v1",
        Description = "Asynchronously ingests Unreal crash reports and searches fingerprinted crash groups."
    });
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<ElasticsearchOptions>(
    builder.Configuration.GetSection(ElasticsearchOptions.SectionName));
builder.Services.AddSingleton<RabbitCrashPublisher>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<RabbitCrashPublisher>());
builder.Services.AddHttpClient<CrashSearchClient>((provider, client) =>
{
    ElasticsearchOptions options = provider.GetRequiredService<IOptions<ElasticsearchOptions>>().Value;
    client.BaseAddress = new Uri(options.Url.TrimEnd('/') + '/', UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHealthChecks()
    .AddCheck<RabbitPublisherHealthCheck>("rabbitmq", tags: new[] { "ready" })
    .AddCheck<ElasticsearchHealthCheck>("elasticsearch", tags: new[] { "ready" });

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
});

ResourceBuilder resource = ResourceBuilder.CreateDefault().AddService(CrashApiTelemetry.ServiceName);
builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.SetResourceBuilder(resource);
    ConfigureLogExporter(options, builder.Configuration);
});
var telemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resources => resources.AddService(CrashApiTelemetry.ServiceName));
telemetry.WithTracing(tracing =>
{
    tracing.AddSource(CrashApiTelemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation();
    ConfigureTraceExporter(tracing, builder.Configuration);
});
telemetry.WithMetrics(metrics =>
{
    metrics.AddMeter(CrashApiTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation();
    ConfigureMetricExporter(metrics, builder.Configuration);
});

var app = builder.Build();
app.UseCors();
app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Crash Intelligence API v1"));

app.MapGet("/", () => Results.Ok(new
{
    service = CrashApiTelemetry.ServiceName,
    version = "1.0",
    openApi = "/openapi/v1.json",
    health = new[] { "/health/live", "/health/ready" }
})).ExcludeFromDescription();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponseAsync
}).ExcludeFromDescription();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
}).ExcludeFromDescription();

app.MapPost("/api/v1/crashes", async (
    UnrealCrashReport? report,
    RabbitCrashPublisher publisher,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var stopwatch = Stopwatch.StartNew();
    using Activity? activity = CrashApiTelemetry.ActivitySource.StartActivity("crash.ingest");
    ILogger logger = loggerFactory.CreateLogger("PipelineGuard.Crash.Ingestion");

    try
    {
        CrashReportValidator.Validate(report);
        UnrealCrashReport validatedReport = report!;
        CrashEnvelope envelope = CrashEnvelope.Create(validatedReport, DateTimeOffset.UtcNow);
        await publisher.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("crash.message_id", envelope.MessageId);
        activity?.SetTag("crash.project", validatedReport.Project);
        CrashApiTelemetry.RecordIngestion("accepted", stopwatch.Elapsed.TotalMilliseconds);
        logger.LogInformation(
            "Accepted crash {CrashId} for asynchronous processing from project {Project} build {Build}",
            validatedReport.CrashId,
            validatedReport.Project,
            validatedReport.Build.Version);
        return Results.Accepted(
            $"/api/v1/crashes/{validatedReport.CrashId:D}",
            new CrashAcceptedResponse(validatedReport.CrashId, "queued", validatedReport.CrashId));
    }
    catch (CrashReportValidationException exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "invalid_report");
        CrashApiTelemetry.RecordIngestion("invalid", stopwatch.Elapsed.TotalMilliseconds);
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["report"] = exception.Errors.ToArray()
        });
    }
    catch (CrashBrokerUnavailableException exception)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "broker_unavailable");
        CrashApiTelemetry.RecordIngestion("broker_unavailable", stopwatch.Elapsed.TotalMilliseconds);
        logger.LogError(exception, "Could not queue crash report");
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Crash ingestion is temporarily unavailable.",
            extensions: new Dictionary<string, object?> { ["retryable"] = true });
    }
})
.WithName("IngestCrash")
.WithSummary("Queue an Unreal crash report")
.WithDescription("Validates a v1 report, publishes a persistent RabbitMQ message, waits for publisher confirmation, and returns 202. crashId is the idempotency key.")
.Accepts<UnrealCrashReport>("application/json")
.Produces<CrashAcceptedResponse>(StatusCodes.Status202Accepted)
.ProducesValidationProblem()
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/api/v1/crashes/{crashId:guid}", async (
    Guid crashId,
    CrashSearchClient search,
    CancellationToken cancellationToken) =>
{
    try
    {
        CrashDocument? crash = await search.GetCrashAsync(crashId, cancellationToken).ConfigureAwait(false);
        return crash is null ? Results.NotFound(new { error = "crash_not_found" }) : Results.Ok(crash);
    }
    catch (Exception exception) when (exception is CrashSearchUnavailableException or HttpRequestException)
    {
        return SearchUnavailable(exception);
    }
})
.WithName("GetCrash")
.WithSummary("Get one processed crash")
.Produces<CrashDocument>()
.Produces(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/api/v1/crash-groups", async (
    string? query,
    string? project,
    string? build,
    string? platform,
    string? status,
    int? limit,
    CrashSearchClient search,
    CancellationToken cancellationToken) =>
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        var request = new CrashGroupSearchRequest(query, project, build, platform, status, limit);
        CrashGroupSearchResult result = await search.SearchGroupsAsync(request, cancellationToken)
            .ConfigureAwait(false);
        CrashApiTelemetry.RecordSearch("success", stopwatch.Elapsed.TotalMilliseconds);
        return Results.Ok(result);
    }
    catch (Exception exception) when (exception is CrashSearchUnavailableException or HttpRequestException)
    {
        CrashApiTelemetry.RecordSearch("failure", stopwatch.Elapsed.TotalMilliseconds);
        return SearchUnavailable(exception);
    }
})
.WithName("SearchCrashGroups")
.WithSummary("Search and aggregate crash groups")
.WithDescription("Supports full-text search plus exact project, build, platform, and regression-status filters.")
.Produces<CrashGroupSearchResult>()
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapPut("/api/v1/crash-groups/{fingerprint}/resolution", async (
    string fingerprint,
    ResolveCrashGroupRequest? request,
    CrashSearchClient search,
    CancellationToken cancellationToken) =>
{
    if (!Regex.IsMatch(fingerprint, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant) ||
        request is null ||
        string.IsNullOrWhiteSpace(request.ResolvedInBuild) ||
        request.ResolvedInBuild.Trim().Length > 100 ||
        request.Note?.Length > 500)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["resolution"] = new[] { "A 64-character lowercase fingerprint and a resolvedInBuild of at most 100 characters are required." }
        });
    }

    try
    {
        CrashGroupResolution resolution = await search.ResolveGroupAsync(
            fingerprint,
            request,
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(resolution);
    }
    catch (Exception exception) when (exception is CrashSearchUnavailableException or HttpRequestException)
    {
        return SearchUnavailable(exception);
    }
})
.WithName("ResolveCrashGroup")
.WithSummary("Mark a crash group resolved")
.WithDescription("A later crash occurrence after resolvedAtUtc is automatically reported as a regression.")
.Produces<CrashGroupResolution>()
.ProducesValidationProblem()
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

await app.RunAsync().ConfigureAwait(false);

static IResult SearchUnavailable(Exception exception) => Results.Problem(
    statusCode: StatusCodes.Status503ServiceUnavailable,
    title: "Crash search is temporarily unavailable.",
    detail: exception.Message,
    extensions: new Dictionary<string, object?> { ["retryable"] = true });

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
