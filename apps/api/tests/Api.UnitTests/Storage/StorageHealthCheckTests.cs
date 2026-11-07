using Api.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedKernel;

namespace Api.UnitTests.Storage;

// Section 7, "File storage", rule 7: the worker's heartbeat runs the readiness checks every interval, so the storage's check calls the
// bucket until it passes once, and no more after that. The store is a hand-written fake of our port: these tests count its calls.
public sealed class StorageHealthCheckTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CheckHealth_Twice_UsesTheStoreOnce()
    {
        var store = new FakeFileStore();
        var check = new StorageHealthCheck(store);

        await check.CheckHealthAsync(new HealthCheckContext(), Cancellation);
        var second = await check.CheckHealthAsync(new HealthCheckContext(), Cancellation);

        second.Status.ShouldBe(HealthStatus.Healthy);
        store.Saved.ShouldHaveSingleItem().ShouldStartWith(FileKeys.Health);
        store.Deleted.ShouldBe(store.Saved);
    }

    [Fact]
    public async Task CheckHealth_AfterAFailure_TriesAgain()
    {
        var store = new FakeFileStore { FailuresLeft = 1 };
        var check = new StorageHealthCheck(store);

        var first = () => check.CheckHealthAsync(new HealthCheckContext(), Cancellation);
        await first.ShouldThrowAsync<IOException>();
        var second = await check.CheckHealthAsync(new HealthCheckContext(), Cancellation);

        second.Status.ShouldBe(HealthStatus.Healthy);
        store.Saved.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CheckHealth_StoreGivesBackAnotherContent_IsUnhealthyAndTriesAgain()
    {
        var store = new FakeFileStore { Content = "another"u8.ToArray() };
        var check = new StorageHealthCheck(store);

        var first = await check.CheckHealthAsync(new HealthCheckContext(), Cancellation);
        await check.CheckHealthAsync(new HealthCheckContext(), Cancellation);

        first.Status.ShouldBe(HealthStatus.Unhealthy);
        store.Saved.Count.ShouldBe(2);
    }

    // Keeps what it is given, or gives back Content when a test sets it. A save fails while FailuresLeft is above zero.
    private sealed class FakeFileStore : IFileStore
    {
        private byte[] _saved = [];

        public int FailuresLeft { get; set; }

        public byte[]? Content { get; init; }

        public List<string> Saved { get; } = [];

        public List<string> Deleted { get; } = [];

        public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        {
            Saved.Add(key);
            if (FailuresLeft-- > 0)
            {
                throw new IOException("The storage does not answer.");
            }

            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            _saved = copy.ToArray();
        }

        public Task<Stream> ReadAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(Content ?? _saved));

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            Deleted.Add(key);
            return Task.CompletedTask;
        }

        public Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Uri> CreateDownloadLinkAsync(string key, string fileName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
