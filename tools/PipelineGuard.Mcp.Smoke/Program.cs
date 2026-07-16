using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine(
        "Usage: PipelineGuard.Mcp.Smoke <PipelineGuard.Mcp.dll> <api-url> [run-id]");
    return 2;
}

string serverAssembly = Path.GetFullPath(args[0]);
if (!File.Exists(serverAssembly))
{
    Console.Error.WriteLine($"MCP server assembly not found: {serverAssembly}");
    return 2;
}
if (!Uri.TryCreate(args[1], UriKind.Absolute, out Uri? apiUri) || !apiUri.IsLoopback)
{
    Console.Error.WriteLine("The smoke client requires a loopback API URL.");
    return 2;
}
if (args.Length == 3 && !Guid.TryParse(args[2], out _))
{
    Console.Error.WriteLine("run-id must be a GUID.");
    return 2;
}

Dictionary<string, string?> environment =
    StdioClientTransportOptions.GetDefaultEnvironmentVariables();
environment["DOTNET_ROLL_FORWARD"] = "LatestMajor";
environment["PIPELINEGUARD_API_URL"] = apiUri.AbsoluteUri;

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "PipelineGuard smoke",
    Command = "dotnet",
    Arguments = [serverAssembly],
    WorkingDirectory = Path.GetDirectoryName(serverAssembly),
    InheritEnvironmentVariables = false,
    EnvironmentVariables = environment,
    ShutdownTimeout = TimeSpan.FromSeconds(5)
});

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await using McpClient client = await McpClient.CreateAsync(
    transport,
    cancellationToken: timeout.Token).ConfigureAwait(false);

IList<McpClientTool> tools = await client.ListToolsAsync(
    cancellationToken: timeout.Token).ConfigureAwait(false);
string[] actualToolNames = tools
    .Select(tool => tool.Name)
    .OrderBy(name => name, StringComparer.Ordinal)
    .ToArray();
string[] expectedToolNames =
[
    "validation_get_remediation",
    "validation_get_summary",
    "validation_list_findings",
    "validation_list_runs"
];
if (!actualToolNames.SequenceEqual(expectedToolNames, StringComparer.Ordinal))
{
    throw new InvalidOperationException(
        $"Unexpected MCP tool set: {string.Join(", ", actualToolNames)}");
}

CallToolResult listResult = await client.CallToolAsync(
    "validation_list_runs",
    new Dictionary<string, object?>
    {
        ["limit"] = 5,
        ["offset"] = 0
    },
    cancellationToken: timeout.Token).ConfigureAwait(false);
if (listResult.IsError == true)
{
    throw new InvalidOperationException("validation_list_runs returned an MCP tool error.");
}

Console.WriteLine(
    $"MCP initialized; tools={actualToolNames.Length}; names={string.Join(",", actualToolNames)}; listContent={listResult.Content.Count}");

if (args.Length == 3)
{
    CallToolResult summaryResult = await client.CallToolAsync(
        "validation_get_summary",
        new Dictionary<string, object?> { ["runId"] = args[2] },
        cancellationToken: timeout.Token).ConfigureAwait(false);
    if (summaryResult.IsError == true)
    {
        throw new InvalidOperationException("validation_get_summary returned an MCP tool error.");
    }

    Console.WriteLine($"MCP summary call succeeded; content={summaryResult.Content.Count}");
}

return 0;
