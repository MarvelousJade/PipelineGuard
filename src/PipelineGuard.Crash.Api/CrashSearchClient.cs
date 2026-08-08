using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using PipelineGuard.CrashIntelligence;

namespace PipelineGuard.Crash.Api;

public sealed class CrashSearchClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ElasticsearchOptions _options;

    public CrashSearchClient(HttpClient httpClient, IOptions<ElasticsearchOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient
            .GetAsync("_cluster/health?local=true", cancellationToken)
            .ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<CrashDocument?> GetCrashAsync(Guid crashId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient
            .GetAsync($"{_options.CrashIndex}/_doc/{crashId:D}", cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, "retrieve a crash", cancellationToken).ConfigureAwait(false);
        JsonNode root = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        return root["_source"]?.Deserialize<CrashDocument>(SerializerOptions);
    }

    public async Task<CrashGroupSearchResult> SearchGroupsAsync(
        CrashGroupSearchRequest request,
        CancellationToken cancellationToken)
    {
        int limit = Math.Clamp(request.Limit ?? 50, 1, 100);
        int aggregationSize = request.Status is null ? limit : 500;
        JsonArray filters = BuildFilters(request);
        var must = new JsonArray();
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            must.Add(new JsonObject
            {
                ["multi_match"] = new JsonObject
                {
                    ["query"] = request.Query.Trim(),
                    ["fields"] = new JsonArray("errorMessage", "errorType", "normalizedStackTrace")
                }
            });
        }

        var body = new JsonObject
        {
            ["size"] = 0,
            ["track_total_hits"] = true,
            ["query"] = new JsonObject
            {
                ["bool"] = new JsonObject
                {
                    ["must"] = must,
                    ["filter"] = filters
                }
            },
            ["aggs"] = new JsonObject
            {
                ["groups"] = new JsonObject
                {
                    ["terms"] = new JsonObject
                    {
                        ["field"] = "fingerprint",
                        ["size"] = aggregationSize,
                        ["order"] = new JsonObject { ["lastSeen"] = "desc" }
                    },
                    ["aggs"] = new JsonObject
                    {
                        ["firstSeen"] = new JsonObject { ["min"] = new JsonObject { ["field"] = "occurredAtUtc" } },
                        ["lastSeen"] = new JsonObject { ["max"] = new JsonObject { ["field"] = "occurredAtUtc" } },
                        ["affectedBuildCount"] = new JsonObject { ["cardinality"] = new JsonObject { ["field"] = "build.version" } },
                        ["builds"] = new JsonObject
                        {
                            ["terms"] = new JsonObject { ["field"] = "build.version", ["size"] = 20 }
                        },
                        ["example"] = new JsonObject
                        {
                            ["top_hits"] = new JsonObject
                            {
                                ["size"] = 1,
                                ["sort"] = new JsonArray(new JsonObject
                                {
                                    ["occurredAtUtc"] = new JsonObject { ["order"] = "desc" }
                                }),
                                ["_source"] = new JsonArray("errorType", "errorMessage", "normalizedStackTrace")
                            }
                        }
                    }
                }
            }
        };

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            $"{_options.CrashIndex}/_search",
            body,
            SerializerOptions,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new CrashGroupSearchResult(0, 0, Array.Empty<CrashGroupSummary>());
        }

        await EnsureSuccessAsync(response, "search crash groups", cancellationToken).ConfigureAwait(false);
        JsonNode root = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        long total = root["hits"]?["total"]?["value"]?.GetValue<long>() ?? 0;
        JsonArray buckets = root["aggregations"]?["groups"]?["buckets"]?.AsArray() ?? new JsonArray();
        string[] fingerprints = buckets
            .Select(bucket => bucket?["key"]?.GetValue<string>())
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
        IReadOnlyDictionary<string, CrashGroupResolution> resolutions =
            await GetResolutionsAsync(fingerprints, cancellationToken).ConfigureAwait(false);

        var groups = new List<CrashGroupSummary>(buckets.Count);
        foreach (JsonNode? bucket in buckets)
        {
            if (bucket is null)
            {
                continue;
            }

            string fingerprint = bucket["key"]?.GetValue<string>() ?? string.Empty;
            long occurrences = bucket["doc_count"]?.GetValue<long>() ?? 0;
            DateTimeOffset firstSeen = ReadDate(bucket["firstSeen"]?["value_as_string"]);
            DateTimeOffset lastSeen = ReadDate(bucket["lastSeen"]?["value_as_string"]);
            JsonNode? source = bucket["example"]?["hits"]?["hits"]?[0]?["_source"];
            string representativeFrame = source?["normalizedStackTrace"]?[0]?.GetValue<string>() ?? string.Empty;
            IReadOnlyList<string> builds = bucket["builds"]?["buckets"]?.AsArray()
                .Select(item => item?["key"]?.GetValue<string>())
                .Where(value => value is not null)
                .Cast<string>()
                .ToArray() ?? Array.Empty<string>();
            resolutions.TryGetValue(fingerprint, out CrashGroupResolution? resolution);
            string status = GetRegressionStatus(occurrences, lastSeen, resolution);

            if (!string.IsNullOrWhiteSpace(request.Status) &&
                !string.Equals(status, request.Status, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            groups.Add(new CrashGroupSummary(
                fingerprint,
                occurrences,
                firstSeen,
                lastSeen,
                bucket["affectedBuildCount"]?["value"]?.GetValue<long>() ?? 0,
                builds,
                source?["errorType"]?.GetValue<string>() ?? string.Empty,
                source?["errorMessage"]?.GetValue<string>() ?? string.Empty,
                representativeFrame,
                status,
                resolution?.ResolvedInBuild));

            if (groups.Count == limit)
            {
                break;
            }
        }

        return new CrashGroupSearchResult(total, groups.Count, groups);
    }

    public async Task<CrashGroupResolution> ResolveGroupAsync(
        string fingerprint,
        ResolveCrashGroupRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureResolutionIndexAsync(cancellationToken).ConfigureAwait(false);
        var resolution = new CrashGroupResolution(
            fingerprint,
            DateTimeOffset.UtcNow,
            request.ResolvedInBuild.Trim(),
            string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim());
        using HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
            $"{_options.ResolutionIndex}/_doc/{Uri.EscapeDataString(fingerprint)}?refresh=wait_for",
            resolution,
            SerializerOptions,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "resolve a crash group", cancellationToken).ConfigureAwait(false);
        return resolution;
    }

    private async Task<IReadOnlyDictionary<string, CrashGroupResolution>> GetResolutionsAsync(
        IReadOnlyList<string> fingerprints,
        CancellationToken cancellationToken)
    {
        if (fingerprints.Count == 0)
        {
            return new Dictionary<string, CrashGroupResolution>(StringComparer.Ordinal);
        }

        var body = new JsonObject
        {
            ["ids"] = new JsonArray(fingerprints
                .Select(value => (JsonNode?)JsonValue.Create(value))
                .ToArray())
        };
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            $"{_options.ResolutionIndex}/_mget",
            body,
            SerializerOptions,
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new Dictionary<string, CrashGroupResolution>(StringComparer.Ordinal);
        }

        await EnsureSuccessAsync(response, "retrieve crash group resolutions", cancellationToken)
            .ConfigureAwait(false);
        JsonNode root = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<string, CrashGroupResolution>(StringComparer.Ordinal);
        foreach (JsonNode? document in root["docs"]?.AsArray() ?? new JsonArray())
        {
            if (document?["found"]?.GetValue<bool>() != true)
            {
                continue;
            }

            CrashGroupResolution? resolution =
                document["_source"]?.Deserialize<CrashGroupResolution>(SerializerOptions);
            if (resolution is not null)
            {
                result[resolution.Fingerprint] = resolution;
            }
        }

        return result;
    }

    private async Task EnsureResolutionIndexAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage head = await _httpClient
            .SendAsync(new HttpRequestMessage(HttpMethod.Head, _options.ResolutionIndex), cancellationToken)
            .ConfigureAwait(false);
        if (head.IsSuccessStatusCode)
        {
            return;
        }

        const string mapping = """
        {
          "mappings": {
            "dynamic": "strict",
            "properties": {
              "fingerprint": { "type": "keyword" },
              "resolvedAtUtc": { "type": "date" },
              "resolvedInBuild": { "type": "keyword" },
              "note": { "type": "text" }
            }
          }
        }
        """;
        using var request = new HttpRequestMessage(HttpMethod.Put, _options.ResolutionIndex)
        {
            Content = new StringContent(mapping, System.Text.Encoding.UTF8, "application/json")
        };
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string details = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.BadRequest &&
            details.Contains("resource_already_exists_exception", StringComparison.Ordinal))
        {
            return;
        }

        throw new CrashSearchUnavailableException(
            $"Elasticsearch could not create the resolution index (HTTP {(int)response.StatusCode}): {details}");
    }

    private static JsonArray BuildFilters(CrashGroupSearchRequest request)
    {
        var filters = new JsonArray();
        AddTerm(filters, "project", request.Project);
        AddTerm(filters, "build.version", request.Build);
        AddTerm(filters, "platform", request.Platform);
        return filters;
    }

    private static void AddTerm(JsonArray filters, string field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(new JsonObject
            {
                ["term"] = new JsonObject { [field] = value.Trim() }
            });
        }
    }

    private static string GetRegressionStatus(
        long occurrences,
        DateTimeOffset lastSeen,
        CrashGroupResolution? resolution)
    {
        if (resolution is not null)
        {
            return lastSeen > resolution.ResolvedAtUtc ? "regression" : "resolved";
        }

        return occurrences == 1 ? "new" : "ongoing";
    }

    private static DateTimeOffset ReadDate(JsonNode? node) =>
        DateTimeOffset.TryParse(node?.GetValue<string>(), out DateTimeOffset value)
            ? value
            : DateTimeOffset.UnixEpoch;

    private static async Task<JsonNode> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? new JsonObject();
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string details = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new CrashSearchUnavailableException(
            $"Elasticsearch could not {operation} (HTTP {(int)response.StatusCode}): {details}");
    }
}
