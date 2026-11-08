using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Fixtures;

// The condition that ends a tracked session (SettledMessages). The records come in the order one host writes them; the first test is
// the order of an invitation's sign-up, where the identity provider's answer cascades to the step that records it.
public sealed class SettledMessagesTests
{
    [Fact]
    public void EndSession_ACascadeSentBeforeItsParentSucceeds_WaitsUntilTheCascadeSucceeds()
    {
        var settled = new SettledMessages();
        var open = Local("open-sign-up");
        var record = Local("record-sign-up");
        var whileTheParentRuns = new List<bool>();

        settled.Record(new EnvelopeRecord(MessageEventType.Sent, open, 0, null));
        settled.Record(new EnvelopeRecord(MessageEventType.Received, open, 0, null));
        settled.Record(new EnvelopeRecord(MessageEventType.ExecutionStarted, open, 0, null));
        settled.Record(new EnvelopeRecord(MessageEventType.ExecutionFinished, open, 0, null));
        whileTheParentRuns.Add(settled.IsCompleted());
        settled.Record(new EnvelopeRecord(MessageEventType.Sent, record, 0, null));
        settled.Record(new EnvelopeRecord(MessageEventType.MessageSucceeded, open, 0, null));
        whileTheParentRuns.Add(settled.IsCompleted());
        settled.Record(new EnvelopeRecord(MessageEventType.Received, record, 0, null));
        settled.Record(new EnvelopeRecord(MessageEventType.ExecutionStarted, record, 0, null));
        settled.Record(new EnvelopeRecord(MessageEventType.ExecutionFinished, record, 0, null));
        whileTheParentRuns.Add(settled.IsCompleted());
        settled.Record(new EnvelopeRecord(MessageEventType.MessageSucceeded, record, 0, null));

        whileTheParentRuns.ShouldBe([false, false, false]);
        settled.IsCompleted().ShouldBeTrue();
    }

    [Fact]
    public void EndSession_AMessageForLaterAndOneForAnotherTransport_EndsOnceTheyAreSent()
    {
        var settled = new SettledMessages();
        var later = Local("expire-sign-up");
        later.Status = EnvelopeStatus.Scheduled;

        settled.Record(new EnvelopeRecord(MessageEventType.Sent, later, 0, null));
        settled.Record(new EnvelopeRecord(MessageEventType.Sent, new Envelope(new object()) { Destination = new Uri("postgresql://messages/") }, 0, null));

        settled.IsCompleted().ShouldBeTrue();
    }

    [Fact]
    public void EndSession_AnInvokedMessage_WaitsUntilItsExecutionFinishes()
    {
        var settled = new SettledMessages();
        var invoked = new Envelope(new object());

        settled.Record(new EnvelopeRecord(MessageEventType.ExecutionStarted, invoked, 0, null));
        var whileItRuns = settled.IsCompleted();
        settled.Record(new EnvelopeRecord(MessageEventType.ExecutionFinished, invoked, 0, null));

        whileItRuns.ShouldBeFalse();
        settled.IsCompleted().ShouldBeTrue();
    }

    [Fact]
    public void EndSession_AFailedMessageRequeued_KeepsWaiting()
    {
        var settled = new SettledMessages();
        var retried = Local("record-sign-up");

        settled.Record(new EnvelopeRecord(MessageEventType.Sent, retried, 0, null));
        settled.Record(new EnvelopeRecord(MessageEventType.Received, retried, 0, null));
        settled.Record(new EnvelopeRecord(MessageEventType.Requeued, retried, 0, null));

        settled.IsCompleted().ShouldBeFalse();
    }

    private static Envelope Local(string queue) => new(new object()) { Destination = new Uri($"local://{queue}/") };
}
