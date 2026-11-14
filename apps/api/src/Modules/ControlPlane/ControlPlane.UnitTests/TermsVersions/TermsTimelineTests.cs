using ControlPlane.Domain.TermsVersions;

namespace ControlPlane.UnitTests.TermsVersions;

public sealed class TermsTimelineTests
{
    private static readonly DateOnly Today = new(2026, 11, 14);

    private static readonly DateTimeOffset Created = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid First = Guid.CreateVersion7();

    private static readonly Guid Second = Guid.CreateVersion7();

    private static readonly Guid Next = Guid.CreateVersion7();

    private static readonly Guid Dropped = Guid.CreateVersion7();

    [Fact]
    public void ReadTermsTimeline_VersionsOfEveryKind_GivesEachItsState()
    {
        var timeline = TermsTimeline.Of(
            [
                new(First, new DateOnly(2026, 1, 1), Created, false),
                new(Second, new DateOnly(2026, 6, 1), Created, false),
                new(Next, new DateOnly(2026, 12, 1), Created, false),
                new(Dropped, new DateOnly(2026, 11, 20), Created, true),
            ],
            Today);

        new[] { First, Second, Next, Dropped }.Select(timeline.StateOf).ShouldBe(
            [TermsVersionState.Replaced, TermsVersionState.Current, TermsVersionState.Scheduled, TermsVersionState.Cancelled]);
        (timeline.CurrentId, timeline.ScheduledId).ShouldBe((Second, Next));
    }

    [Fact]
    public void ReadTermsTimeline_AVersionOfToday_IsInForce()
    {
        var timeline = TermsTimeline.Of([new(First, new DateOnly(2026, 1, 1), Created, false), new(Second, Today, Created, false)], Today);

        (timeline.CurrentId, timeline.StateOf(First)).ShouldBe((Second, TermsVersionState.Replaced));
    }

    [Fact]
    public void ReadTermsTimeline_TwoVersionsOfOneDay_PutsTheLaterCreatedInForce()
    {
        var timeline = TermsTimeline.Of([new(First, Today, Created.AddHours(1), false), new(Second, Today, Created, false)], Today);

        (timeline.CurrentId, timeline.StateOf(Second)).ShouldBe((First, TermsVersionState.Replaced));
    }

    [Fact]
    public void ReadTermsTimeline_ACancelledVersionWhoseDayCame_LeavesTheEarlierInForce()
    {
        var timeline = TermsTimeline.Of([new(First, new DateOnly(2026, 1, 1), Created, false), new(Dropped, Today, Created, true)], Today);

        (timeline.CurrentId, timeline.StateOf(Dropped)).ShouldBe((First, TermsVersionState.Cancelled));
    }

    [Fact]
    public void ReadTermsTimeline_OnlyAScheduledVersion_HasNoneInForce()
    {
        var timeline = TermsTimeline.Of([new(Next, new DateOnly(2026, 12, 1), Created, false)], Today);

        (timeline.CurrentId, timeline.ScheduledId).ShouldBe((null, Next));
    }
}
