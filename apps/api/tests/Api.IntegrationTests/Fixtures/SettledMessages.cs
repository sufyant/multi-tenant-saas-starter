using Wolverine;
using Wolverine.Tracking;
using Wolverine.Transports;

namespace Api.IntegrationTests.Fixtures;

// The end of a tracked session: every message the session saw has been handled. Wolverine's own check reads a snapshot of its envelope
// histories, then each history as it is at that later moment. A handler that cascades can therefore succeed after the snapshot while its
// cascade is missing from it, and the session ends before the cascade runs. This condition keeps every record under one lock, so it
// never reads a state between two records. Its rules are Wolverine's own for one host (EnvelopeHistory.RecordLocally in WolverineFx
// 6.51): a message sent to a local queue waits until it is received, a received one until it succeeds or fails, an invoked one until
// its execution finishes; a message for another transport or for later is done once sent. With a condition, the session asks only the
// condition.
internal sealed class SettledMessages : ITrackedCondition
{
    // Wolverine's message type of a scheduled envelope (TransportConstants.ScheduledEnvelope, which is internal).
    private const string ScheduledEnvelope = "scheduled-envelope";

    private readonly Lock _lock = new();

    private readonly Dictionary<Guid, List<Entry>> _envelopes = [];

    public void Record(EnvelopeRecord record)
    {
        if (record.Envelope is not { } envelope)
        {
            return;
        }

        lock (_lock)
        {
            if (!_envelopes.TryGetValue(envelope.Id, out var entries))
            {
                entries = [];
                _envelopes[envelope.Id] = entries;
            }

            entries.Add(Apply(record.MessageEventType, envelope, entries));
        }
    }

    public bool IsCompleted()
    {
        lock (_lock)
        {
            return _envelopes.Values.All(entries => entries.All(entry => entry.Complete));
        }
    }

    private static Entry Apply(MessageEventType type, Envelope envelope, List<Entry> entries)
    {
        switch (type)
        {
            case MessageEventType.Sent:
                return new Entry(type)
                {
                    Complete = envelope.Destination?.Scheme != TransportConstants.Local
                        || envelope.MessageType == ScheduledEnvelope
                        || envelope.Status == EnvelopeStatus.Scheduled,
                };

            case MessageEventType.Received:
                if (envelope.Destination?.Scheme == TransportConstants.Local)
                {
                    CompleteLast(entries, MessageEventType.Sent);
                }

                return new Entry(type);

            case MessageEventType.ExecutionFinished:
                CompleteLast(entries, MessageEventType.ExecutionStarted);
                return new Entry(type) { Complete = true };

            case MessageEventType.MessageSucceeded:
            case MessageEventType.MessageFailed:
            case MessageEventType.MovedToErrorQueue:
            case MessageEventType.Discarded:
            case MessageEventType.NoHandlers:
            case MessageEventType.NoRoutes:
                entries.ForEach(entry => entry.Complete = true);
                return new Entry(type) { Complete = true };

            case MessageEventType.ExecutionStarted:
            case MessageEventType.Requeued:
                return new Entry(type);

            default:
                return new Entry(type) { Complete = true };
        }
    }

    private static void CompleteLast(List<Entry> entries, MessageEventType type)
    {
        if (entries.LastOrDefault(entry => entry.Type == type) is { } last)
        {
            last.Complete = true;
        }
    }

    private sealed record Entry(MessageEventType Type)
    {
        public bool Complete { get; set; }
    }
}
