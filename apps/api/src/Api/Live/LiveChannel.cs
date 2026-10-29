using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Api.Caching;
using SharedKernel;
using StackExchange.Redis;

namespace Api.Live;

// The live channel of U6. Each process keeps the open streams of its users. Without Valkey, an event goes straight to the streams of this
// process. With Valkey, an event goes to one channel of Valkey's publish and subscribe, and each process that has an open stream hears every
// event of it and hands each to its own streams; a worker only publishes. A failure of Valkey loses the event and logs a warning: it never
// fails the caller, because the event carries no content and the next read is right (section 7, "Error tracking", rule 4).
internal sealed partial class LiveChannel(ILogger<LiveChannel> logger, ValkeyConnection? valkey) : ILiveChannel, IDisposable
{
    // One channel of Valkey for every user: a process hears it once, however many streams it holds.
    private static readonly RedisChannel Channel = RedisChannel.Literal($"{CachingExtensions.KeyPrefix}live");

    // A stream that falls behind keeps the newest events: each is a signal to read again, so an older one adds nothing.
    private static readonly BoundedChannelOptions StreamBuffer = new(16) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true };

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Channel<LiveEvent>, byte>> _streams = new();

    private readonly SemaphoreSlim _subscribing = new(1, 1);

    private bool _subscribed;

    public void Dispose() => _subscribing.Dispose();

    public async Task PublishAsync(Guid userId, LiveEvent liveEvent, CancellationToken cancellationToken)
    {
        if (valkey is null)
        {
            Deliver(userId, liveEvent);
            return;
        }

        try
        {
            var connection = await valkey.ConnectAsync();
            await connection.GetSubscriber().PublishAsync(Channel, JsonSerializer.Serialize(new Message(userId, liveEvent.Name, liveEvent.Data)));
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException or ObjectDisposedException)
        {
            LogPublishFailed(logger, exception, liveEvent.Name);
        }
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "A live event {Name} was not published, and is lost.")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The process could not subscribe to the live events of other processes; it tries again at the next stream.")]
    private static partial void LogSubscribeFailed(ILogger logger, Exception exception);

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
