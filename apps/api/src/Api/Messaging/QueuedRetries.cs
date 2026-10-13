using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Runtime;

namespace Api.Messaging;

internal sealed class QueuedRetries : IContinuationSource, IWithFailurePolicies
{
    public QueuedRetries() => this.OnAnyException()
        .RetryWithCooldown(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2))
        .Then.MoveToErrorQueue();

    public FailureRuleCollection Failures { get; } = new();
    public string Description => "Retry queued messages three times inline, then move them to the dead letter queue";

    public IContinuation? Build(Exception ex, Envelope envelope)
    {
        // Inline InvokeAsync envelopes have no destination. Only a received message has a queue destination.
        return envelope.Destination is not null && Failures.Single().TryCreateContinuation(ex, envelope, out var continuation)
            ? continuation : null;
    }
}
