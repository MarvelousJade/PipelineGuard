using PipelineGuard.Crash.Worker;

namespace PipelineGuard.CrashIntelligence.Tests;

public sealed class CrashEventProcessorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 16, 12, 5, 0, TimeSpan.Zero);

    [Fact]
    public async Task DuplicateDelivery_CreatesExactlyOneDocument()
    {
        var index = new InMemoryCrashIndex();
        var processor = new CrashEventProcessor(index, new FixedTimeProvider(Now));
        CrashEnvelope envelope = CrashTestData.Envelope();

        CrashIndexWriteOutcome first = await processor.ProcessAsync(envelope, TestContext.Current.CancellationToken);
        CrashIndexWriteOutcome replay = await processor.ProcessAsync(envelope, TestContext.Current.CancellationToken);

        Assert.Equal(CrashIndexWriteOutcome.Created, first);
        Assert.Equal(CrashIndexWriteOutcome.Duplicate, replay);
        Assert.Equal(1, index.DocumentCount);
    }

    [Fact]
    public async Task TransientFailure_CanBeRetriedWithoutDuplicateEffects()
    {
        var index = new InMemoryCrashIndex(failFirstWrite: true);
        var processor = new CrashEventProcessor(index, new FixedTimeProvider(Now));
        CrashEnvelope envelope = CrashTestData.Envelope();

        await Assert.ThrowsAsync<CrashIndexTransientException>(() =>
            processor.ProcessAsync(envelope, TestContext.Current.CancellationToken));
        CrashIndexWriteOutcome retry = await processor.ProcessAsync(
            envelope,
            TestContext.Current.CancellationToken);

        Assert.Equal(CrashIndexWriteOutcome.Created, retry);
        Assert.Equal(1, index.DocumentCount);
    }

    private sealed class InMemoryCrashIndex : ICrashIndex
    {
        private readonly HashSet<Guid> _documents = new();
        private bool _failFirstWrite;

        public InMemoryCrashIndex(bool failFirstWrite = false) => _failFirstWrite = failFirstWrite;

        public int DocumentCount => _documents.Count;

        public Task<CrashIndexWriteOutcome> TryCreateAsync(
            CrashDocument document,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_failFirstWrite)
            {
                _failFirstWrite = false;
                throw new CrashIndexTransientException("Synthetic index outage.");
            }

            return Task.FromResult(_documents.Add(document.CrashId)
                ? CrashIndexWriteOutcome.Created
                : CrashIndexWriteOutcome.Duplicate);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
