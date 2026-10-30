using Api.Caching;
using Api.Hosting;
using Api.Live;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
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

    // A publish never waits for Valkey: the commit that signals takes no time of a stopped Valkey. The queued events fail once each, and
    // the warning comes once, until a send succeeds again.
    [Fact]
    public async Task Publish_ValkeyStopped_ReturnsAtOnceLogsOneWarningAndFailsNobody()
    {
        var logger = new FakeLogger<LiveChannel>();
        using var worker = Channel(logger);
        await _valkey.StopAsync();
        var started = TimeProvider.System.GetTimestamp();

        await worker.PublishAsync(Guid.CreateVersion7(), Signal, TestContext.Current.CancellationToken);
        await worker.PublishAsync(Guid.CreateVersion7(), Signal, TestContext.Current.CancellationToken);
        var took = TimeProvider.System.GetElapsedTime(started);
        await Waiting.UntilAsync(() => logger.Collector.Count > 0);
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        took.ShouldBeLessThan(TimeSpan.FromMilliseconds(100));
        logger.Collector.GetSnapshot().Select(log => (log.Level, log.Message)).ShouldBe(
            [(LogLevel.Warning, "A live event notifications was not published, and is lost; the events after it are lost too until Valkey answers again.")]);
    }

    // A web or a worker process without a cache connection loses the worker's signals; it says so once at start. The role all needs none.
    [Theory]
    [InlineData("Web", null, 1)]
    [InlineData("Worker", null, 1)]
    [InlineData("All", null, 0)]
    [InlineData("Web", "localhost:1", 0)]
    public async Task StartHost_AWebOrAWorkerWithoutACacheConnection_WarnsOnce(string role, string? cache, int warnings)
    {
        var builder = WebApplication.CreateEmptyBuilder(new());
        builder.Configuration[$"ConnectionStrings:{CachingExtensions.CacheConnection}"] = cache;
        builder.Services.AddFakeLogging();
        builder.WebHost.UseTestServer();
        builder.AddLiveChannel(Enum.Parse<HostRole>(role));
        await using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        host.Services.GetRequiredService<FakeLogCollector>().GetSnapshot().Count(log => log.Level == LogLevel.Warning && log.Message.Contains("ConnectionStrings:Cache", StringComparison.Ordinal))
            .ShouldBe(warnings);
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
