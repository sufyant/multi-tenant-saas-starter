using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace Api.IntegrationTests.Fixtures;

// The error tracker is a system we do not own. This transport takes the place of the SDK's HTTP transport, so a test reads every event
// the host would send, as the SDK writes it to the wire, and nothing leaves the process. The DSN names no real project.
internal sealed class ErrorEvents : ITransport
{
    public const string Dsn = "https://public@errors.test/1";

    private readonly ConcurrentQueue<SentryEvent> _events = new();

    public IReadOnlyList<SentryEvent> All => [.. _events];

    // The settings that turn error tracking on, with this release.
    public static Dictionary<string, string?> Settings(string? release = null) => new()
    {
        ["Host:ErrorTracking:Dsn"] = Dsn,
        ["Host:ErrorTracking:Release"] = release,
    };

    // The SDK sends from a queue of its own. A test that expects no event, or no second one, waits until the queue is empty.
    public static Task FlushAsync() => SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));

    public void SendTo(IServiceCollection services) =>
        services.Configure<SentryAspNetCoreOptions>(options => options.Transport = this);

    public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        foreach (var item in envelope.Items.Where(item => item.TryGetType() == "event"))
        {
            using var stream = new MemoryStream();
            await item.SerializeAsync(stream, null, cancellationToken);

            // An item is its header, a line feed and its payload.
            var payload = Encoding.UTF8.GetString(stream.ToArray()).Split('\n', 2)[1];
            using var json = JsonDocument.Parse(payload);
            _events.Enqueue(SentryEvent.FromJson(json.RootElement));
        }
    }

    public List<SentryEvent> Where(Func<SentryEvent, bool> predicate) => [.. _events.Where(predicate)];
}
