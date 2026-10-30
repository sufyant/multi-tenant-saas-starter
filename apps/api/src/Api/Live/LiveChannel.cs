using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Api.Caching;
using SharedKernel;
using StackExchange.Redis;

namespace Api.Live;

// The live channel of U6. Each process keeps the open streams of its users. Without Valkey, an event goes straight to the streams of this
// process. With Valkey, an event goes to one channel of Valkey's publish and subscribe, and each process that has an open stream hears every
// event of it and hands each to its own streams; a worker only publishes. A publish never waits for Valkey: it puts the event in a bounded
// queue of the process, which one loop sends, so a slow or stopped Valkey adds no time to the commit that signals. A failure of Valkey loses
// the event and logs one warning until a send succeeds again: it never fails the caller, because the event carries no content and the next
// read is right (section 7, "Error tracking", rule 4).
internal sealed partial class LiveChannel(ILogger<LiveChannel> logger, ValkeyConnection? valkey) : ILiveChannel, IDisposable
{
    // One channel of Valkey for every user: a process hears it once, however many streams it holds.
    private static readonly RedisChannel Channel = RedisChannel.Literal($"{CachingExtensions.KeyPrefix}live");

    // A stream that falls behind keeps the newest events: each is a signal to read again, so an older one adds nothing.
    private static readonly BoundedChannelOptions StreamBuffer = new(16) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true };

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Channel<LiveEvent>, byte>> _streams = new();

    // The events this process has still to send to Valkey. When Valkey is slow, the oldest go first: each is a signal to read again.
    private readonly Channel<Message> _outgoing =
        System.Threading.Channels.Channel.CreateBounded<Message>(new BoundedChannelOptions(1024) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private readonly CancellationTokenSource _stopping = new();

    private readonly SemaphoreSlim _subscribing = new(1, 1);

    private int _sending;

    private bool _subscribed;

    private bool _failing;

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
        _subscribing.Dispose();
    }

    public Task PublishAsync(Guid userId, LiveEvent liveEvent, CancellationToken cancellationToken)
    {
        if (valkey is null)
        {
            Deliver(userId, liveEvent);
            return Task.CompletedTask;
        }

        _outgoing.Writer.TryWrite(new Message(userId, liveEvent.Name, liveEvent.Data));
        if (Interlocked.Exchange(ref _sending, 1) == 0)
        {
            _ = Task.Run(() => SendAsync(valkey), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<LiveEvent> SubscribeAsync(Guid userId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // The stream is registered before the first wait, so an event that comes once the caller asked for the first one reaches it.
        var stream = System.Threading.Channels.Channel.CreateBounded<LiveEvent>(StreamBuffer);
        var streams = _streams.GetOrAdd(userId, _ => new());
        streams.TryAdd(stream, 0);
        try
        {
            await SubscribeToValkeyAsync();
            while (await WaitAsync(stream.Reader, cancellationToken))
            {
                while (stream.Reader.TryRead(out var liveEvent))
                {
                    yield return liveEvent;
                }
            }
        }
        finally
        {
            streams.TryRemove(stream, out _);
        }
    }

    // The end of the token ends the stream without an exception.
    private static async Task<bool> WaitAsync(ChannelReader<LiveEvent> reader, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.WaitToReadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A live event {Name} was not published, and is lost; the events after it are lost too until Valkey answers again.")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The process could not subscribe to the live events of other processes; it tries again at the next stream.")]
    private static partial void LogSubscribeFailed(ILogger logger, Exception exception);

    // The one loop of the process that sends the queued events to Valkey, each with the connection's time limit (C10).
    private async Task SendAsync(ValkeyConnection connection)
    {
        try
        {
            await foreach (var message in _outgoing.Reader.ReadAllAsync(_stopping.Token))
            {
                try
                {
                    var valkeyConnection = await connection.ConnectAsync();
                    await valkeyConnection.GetSubscriber().PublishAsync(Channel, JsonSerializer.Serialize(message));
                    _failing = false;
                }
                catch (Exception exception) when (exception is RedisException or TimeoutException or ObjectDisposedException)
                {
                    if (!_failing)
                    {
                        LogPublishFailed(logger, exception, message.Name);
                    }

                    _failing = true;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The process stops.
        }
    }

    private void Deliver(Guid userId, LiveEvent liveEvent)
    {
        if (_streams.TryGetValue(userId, out var streams))
        {
            foreach (var stream in streams.Keys)
            {
                stream.Writer.TryWrite(liveEvent);
            }
        }
    }

    // A process subscribes once, at its first stream. StackExchange.Redis subscribes again by itself after a lost connection. A Valkey that
    // is down at the first stream is tried again at the next one.
    private async Task SubscribeToValkeyAsync()
    {
        if (valkey is null || _subscribed)
        {
            return;
        }

        await _subscribing.WaitAsync();
        try
        {
            if (!_subscribed)
            {
                var connection = await valkey.ConnectAsync();
                await connection.GetSubscriber().SubscribeAsync(Channel, (_, value) =>
                {
                    if (value.HasValue && JsonSerializer.Deserialize<Message>((string)value!) is { } message)
                    {
                        Deliver(message.UserId, new LiveEvent(message.Name, message.Data));
                    }
                });
                _subscribed = true;
            }
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException or ObjectDisposedException)
        {
            LogSubscribeFailed(logger, exception);
        }
        finally
        {
            _subscribing.Release();
        }
    }

    private sealed record Message(Guid UserId, string Name, string Data);
}
