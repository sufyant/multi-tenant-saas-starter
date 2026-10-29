namespace SharedKernel;

/// <summary>
/// Sends a signed-in user a live event, on every open stream of theirs in any web process (U6). The host implements it: Valkey's
/// publish and subscribe across processes, or the process itself without a cache connection. It knows no notification, so another live
/// feature can use it; a two-way transport such as SignalR would be another implementation.
/// </summary>
public interface ILiveChannel
{
    /// <summary>
    /// Sends the event to the user's streams. It never fails the caller: an event that cannot be sent is lost and logged as a warning,
    /// and the user's next read is still right, because the event carries no content.
    /// </summary>
    Task PublishAsync(Guid userId, LiveEvent liveEvent, CancellationToken cancellationToken);

    /// <summary>The user's events in this process, from the moment of the call until the token ends.</summary>
    IAsyncEnumerable<LiveEvent> SubscribeAsync(Guid userId, CancellationToken cancellationToken);
}
