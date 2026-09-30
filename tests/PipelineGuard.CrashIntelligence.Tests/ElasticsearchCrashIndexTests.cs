using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PipelineGuard.Crash.Worker;
using PipelineGuard.CrashIntelligence;

namespace PipelineGuard.CrashIntelligence.Tests;

public sealed class ElasticsearchCrashIndexTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 12, 5, 0, TimeSpan.Zero);
    private static CrashDocument Document => CrashDocument.FromEnvelope(CrashTestData.Envelope(), Now);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Replay_PreservesOriginalDocumentThroughHttpAdapter()
    {
        CrashDocument original = Document;
        CrashDocument? stored = null;
        using var handler = new StubHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal($"/crashes/_doc/{original.CrashId:D}", request.RequestUri!.AbsolutePath);
            bool createOnly = request.RequestUri.Query == "?op_type=create";
            if (stored is not null && createOnly) return new HttpResponseMessage(HttpStatusCode.Conflict);
            stored = await request.Content!.ReadFromJsonAsync<CrashDocument>(cancellationToken: token);
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = Client(handler);
        var index = Index(http);

        Assert.Equal(CrashIndexWriteOutcome.Created, await index.TryCreateAsync(original, Token));
        CrashIndexWriteOutcome replay = await index.TryCreateAsync(original with
        {
            ProcessedAtUtc = Now.AddMinutes(1), ErrorMessage = "Changed payload on redelivery"
        }, Token);

        Assert.NotNull(stored);
        Assert.Equal(original.ProcessedAtUtc, stored.ProcessedAtUtc);
        Assert.Equal(original.ErrorMessage, stored.ErrorMessage);
        Assert.Equal(CrashIndexWriteOutcome.Duplicate, replay);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Write_TransientHttpFailureCanBeRetried(int status)
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage((HttpStatusCode)status)));
        using var http = Client(handler);
        await Assert.ThrowsAsync<CrashIndexTransientException>(() => Index(http).TryCreateAsync(Document, Token));
    }

    [Fact]
    public async Task Write_InvalidMappingIsPermanent()
    {
        using var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));
        using var http = Client(handler);
        await Assert.ThrowsAsync<CrashIndexPermanentException>(() => Index(http).TryCreateAsync(Document, Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NetworkFailure_IsTransientForStartupAndWrite(bool startup)
    {
        using var handler = new StubHandler((_, _) => throw new HttpRequestException("Synthetic connection failure"));
        using var http = Client(handler);
        await Assert.ThrowsAsync<CrashIndexTransientException>(() => startup
            ? Index(http).EnsureIndexAsync(Token) : Index(http).TryCreateAsync(Document, Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpTimeout_IsTransientForStartupAndWrite(bool startup)
    {
        // HttpClient wraps its own timeout as cancellation; no real wall-clock delay is required.
        using var handler = new StubHandler((_, _) => throw new TaskCanceledException("Synthetic HTTP timeout"));
        using var http = Client(handler);
        await Assert.ThrowsAsync<CrashIndexTransientException>(() => startup
            ? Index(http).EnsureIndexAsync(Token) : Index(http).TryCreateAsync(Document, Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellation_IsNotClassifiedAsRetryable(bool startup)
    {
        using var handler = new StubHandler((_, _) => throw new InvalidOperationException("Must not send after cancellation"));
        using var http = Client(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startup
            ? Index(http).EnsureIndexAsync(cancellation.Token)
            : Index(http).TryCreateAsync(Document, cancellation.Token));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InFlightCallerCancellation_PropagatesWithoutRetryClassification(bool startup)
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new StubHandler((_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Expected cancellation");
        });
        using var http = Client(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startup
            ? Index(http).EnsureIndexAsync(cancellation.Token)
            : Index(http).TryCreateAsync(Document, cancellation.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task EnsureIndex_ExistingIndexNeedsNoWrite()
    {
        using var handler = new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var http = Client(handler);
        await Index(http).EnsureIndexAsync(Token);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(503)]
    public async Task EnsureIndex_DoesNotCreateAfterFailedNonMissingHead(int status)
    {
        using var handler = new StubHandler((request, _) => Task.FromResult(
            new HttpResponseMessage(request.Method == HttpMethod.Head ? (HttpStatusCode)status : HttpStatusCode.Created)));
        using var http = Client(handler);
        if (status == 403)
            await Assert.ThrowsAsync<CrashIndexPermanentException>(() => Index(http).EnsureIndexAsync(Token));
        else
            await Assert.ThrowsAsync<CrashIndexTransientException>(() => Index(http).EnsureIndexAsync(Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task EnsureIndex_MissingIndexCreatesExpectedMapping()
    {
        using var handler = new StubHandler(async (request, token) =>
        {
            if (request.Method == HttpMethod.Head) return new HttpResponseMessage(HttpStatusCode.NotFound);
            Assert.Equal(HttpMethod.Put, request.Method);
            using JsonDocument mapping = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal("strict", mapping.RootElement.GetProperty("mappings").GetProperty("dynamic").GetString());
            Assert.Equal(0, mapping.RootElement.GetProperty("settings").GetProperty("number_of_replicas").GetInt32());
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = Client(handler);
        await Index(http).EnsureIndexAsync(Token);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task EnsureIndex_ConcurrentCreationIsSafe()
    {
        using var handler = new StubHandler((request, _) => Task.FromResult(request.Method == HttpMethod.Head
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":{\"type\":\"resource_already_exists_exception\"}}")
            }));
        using var http = Client(handler);
        await Index(http).EnsureIndexAsync(Token);
        Assert.Equal(2, handler.Calls);
    }

    private static HttpClient Client(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("http://synthetic-index.invalid/")
    };

    private static ElasticsearchCrashIndex Index(HttpClient http) => new(http,
        Options.Create(new ElasticsearchOptions { CrashIndex = "crashes" }));

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return send(request, token);
        }
    }
}
