using Api.Caching;
using Api.Live;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SharedKernel;
using StackExchange.Redis;

namespace Api.IntegrationTests.Live;

// U6: the live channel of two processes on one Valkey. An event that one process publishes reaches a stream of the user in the other, and
// only that user's. A Valkey that is down loses the event, logs a warning and fails nobody (section 7, "Error tracking", rule 4).
public sealed class LiveChannelTests : IAsyncLifetime
{
    private static readonly LiveEvent Signal = new("notifications", """{"scope":"personal"}""");

    private readonly Valkey _valkey = new();

    public ValueTask InitializeAsync() => _valkey.InitializeAsync();

    public ValueTask DisposeAsync() => _valkey.DisposeAsync();

    [Fact]
    public async Task Publish_InAnotherProcess_ReachesTheUsersStreamAndNoOtherUsers()
    {
        var (user, other) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        using var web = Channel(new FakeLogger<LiveChannel>());
        using var worker = Channel(new FakeLogger<LiveChannel>());
        using var open = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream = web.SubscribeAsync(user, open.Token).GetAsyncEnumerator(open.Token);
        await using var otherStream = web.SubscribeAsync(other, open.Token).GetAsyncEnumerator(open.Token);
        var received = stream.MoveNextAsync().AsTask();
        var otherReceived = otherStream.MoveNextAsync().AsTask();
        await Waiting.UntilAsync(async () =>
        {
            await worker.PublishAsync(user, Signal, CancellationToken.None);
            return received.IsCompleted;
        });

        (await received, stream.Current).ShouldBe((true, Signal));
        otherReceived.IsCompleted.ShouldBeFalse();
        await open.CancelAsync();
        (await otherReceived).ShouldBeFalse();
    }

    [Fact]
    public async Task Publish_ValkeyStopped_LogsAWarningAndFailsNobody()
    {
        var logger = new FakeLogger<LiveChannel>();
        using var worker = Channel(logger);
        await _valkey.StopAsync();

        await worker.PublishAsync(Guid.CreateVersion7(), Signal, TestContext.Current.CancellationToken);

        logger.Collector.GetSnapshot().Select(log => (log.Level, log.Message)).ShouldBe([(LogLevel.Warning, "A live event notifications was not published, and is lost.")]);
    }

    private LiveChannel Channel(ILogger<LiveChannel> logger)
    {
        var options = ConfigurationOptions.Parse(_valkey.ConnectionString);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 1000;
        options.AsyncTimeout = 250;
        var connecting = new Lazy<Task<IConnectionMultiplexer>>(async () => await ConnectionMultiplexer.ConnectAsync(options));
        return new LiveChannel(logger, new ValkeyConnection(connecting));
    }
}
