using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PipelineGuard.Mcp;

internal static class McpTelemetry
{
    public const string ServiceName = "PipelineGuard.Mcp";
    public const string SourceName = "PipelineGuard.Mcp";
    public const string MeterName = "PipelineGuard.Mcp";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> ToolCalls =
        Meter.CreateCounter<long>("pipelineguard.mcp.tool.calls", unit: "{call}");

    private static readonly Histogram<double> ToolDuration =
        Meter.CreateHistogram<double>("pipelineguard.mcp.tool.duration", unit: "ms");

    public static void Record(string tool, string outcome, double elapsedMilliseconds)
    {
        var tags = new TagList
        {
            { "mcp.tool.name", tool },
            { "outcome", outcome }
        };
        ToolCalls.Add(1, tags);
        ToolDuration.Record(elapsedMilliseconds, tags);
    }
}
