namespace Notifications.Application.Notifications;

// The stream of a user's live signals (section Notifications:LiveStream, U6), validated at startup by the module's infrastructure.
public sealed class LiveStreamOptions
{
    public const string Section = "Notifications:LiveStream";

    // A proxy closes a response that sends nothing for a while; Railway closes one after 5 minutes without data. A comment line this
    // often keeps the stream open, and lets the stream see a user who was deleted or suspended.
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(20);

    // How long a stream stays open before it closes, so the client opens a new one with a fresh token. Railway ends a request after
    // 15 minutes even while data flows.
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(5);

    public bool IsValid =>
        KeepAliveInterval > TimeSpan.Zero && KeepAliveInterval <= TimeSpan.FromSeconds(30)
        && Lifetime > KeepAliveInterval && Lifetime <= TimeSpan.FromMinutes(10);
}
