using System.Globalization;
using Api.Observability;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.Postgresql.Transport;
using Wolverine.Runtime;
using Wolverine.Runtime.Interop;
using Wolverine.Transports;

namespace Api.Messaging;

internal static class UnknownMessageOptionsExtensions
{
    public static IServiceCollection AddUnknownMessageOptions(this IServiceCollection services)
    {
        services.AddOptions<UnknownMessageOptions>()
            .BindConfiguration(UnknownMessageOptions.Section)
            .Validate(options => options.Delay > TimeSpan.Zero, $"{UnknownMessageOptions.Section}:Delay must be positive.")
            .Validate(options => options.Tries > 0, $"{UnknownMessageOptions.Section}:Tries must be positive.")
            .ValidateOnStart();

        return services;
    }
}

// How far apart, and how often, a node puts back a message it has no handler for (Host:Messaging:UnknownMessages, W12).
internal sealed class UnknownMessageOptions
{
    public const string Section = "Host:Messaging:UnknownMessages";

    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(30);

    public int Tries { get; set; } = 10;
}

// W12. Wolverine marks the message as handled after this runs, so only the PostgreSQL queue, whose put-back leaves the inbox, can put
// a message back; a durable local queue dead-letters it.
internal sealed partial class UnknownMessages(
    IOptions<UnknownMessageOptions> settings, TimeProvider time, ILogger<UnknownMessages> logger) : IMissingHandler
{
    public const string TriesHeader = "unknown-message-tries";

    public async ValueTask HandleAsync(IEnvelopeLifecycle context, IWolverineRuntime root)
    {
        var envelope = context.Envelope!;
        var scheme = envelope.Destination?.Scheme;
        var tries = envelope.Headers.TryGetValue(TriesHeader, out var value) ? int.Parse(value!, CultureInfo.InvariantCulture) : 0;
        var traceId = TraceIds.OfCurrentTrace();

        if (scheme == PostgresqlTransport.ProtocolName && tries < settings.Value.Tries)
        {
            envelope.Headers[TriesHeader] = (tries + 1).ToString(CultureInfo.InvariantCulture);
            await context.ReScheduleAsync(time.GetUtcNow() + settings.Value.Delay);
            LogPutBack(envelope.MessageType, tries + 1, settings.Value.Tries, settings.Value.Delay, traceId);
            return;
        }

        if (scheme == PostgresqlTransport.ProtocolName || scheme == TransportConstants.Local)
        {
            LogDeadLetter(envelope.MessageType, tries, traceId);
            await context.MoveToDeadLetterQueueAsync(new UnknownMessageTypeNameException($"No handler for message type '{envelope.MessageType}'"));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No handler for {MessageType} on this node. It is put back for try {Try} of {Tries}, in {Delay}. Trace {TraceId}")]
    private partial void LogPutBack(string? messageType, int @try, int tries, TimeSpan delay, string? traceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "No node handled {MessageType} after {Tries} tries. It is moved to the dead letter queue. Trace {TraceId}")]
    private partial void LogDeadLetter(string? messageType, int tries, string? traceId);
}
