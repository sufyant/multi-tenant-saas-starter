using Billing.Domain.Subscriptions;

namespace Billing.UnitTests.Subscriptions;

// B9 and 12.12: the member's change of the period's end that the provider's next state shows, which the Activity log records. A move now,
// another subscription and the closure's own end are no such change.
public sealed class ScheduleChangeTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid Team = Guid.CreateVersion7();

    private static readonly Guid Free = Guid.CreateVersion7();

    private static readonly SubscriptionState Active =
        new("cus_1", "sub_1", Guid.CreateVersion7(), BillingInterval.Monthly, SubscriptionStatus.Active, At.AddMonths(1), CancelAtPeriodEnd: false);

    private static readonly SubscriptionState Cancelled = Active with { CancelAtPeriodEnd = true };

    private static readonly SubscriptionState Scheduled = Active with { ScheduledPlanId = Team, ScheduledInterval = BillingInterval.Monthly };

    public static TheoryData<string, string, string> Transitions => new()
    {
        { nameof(Active), nameof(Cancelled), nameof(ScheduleChange.Cancelled) },
        { nameof(Cancelled), nameof(Active), nameof(ScheduleChange.CancellationUndone) },
        { nameof(Active), nameof(Scheduled), nameof(ScheduleChange.MoveScheduled) },
        { nameof(Scheduled), nameof(Active), nameof(ScheduleChange.MoveUndone) },
        { nameof(Scheduled), nameof(Cancelled), nameof(ScheduleChange.Cancelled) },
        { nameof(Cancelled), nameof(Scheduled), nameof(ScheduleChange.MoveScheduled) },
        { nameof(Active), nameof(Active), "" },
        { nameof(Cancelled), nameof(Cancelled), "" },
        { nameof(Scheduled), nameof(Scheduled), "" },
    };

    [Theory]
    [MemberData(nameof(Transitions))]
    public void ScheduleChangeOf_AMembersChange_IsTheChange(string from, string to, string expected)
    {
        var subscription = Subscription.Start(StateOf(from), At);

        // A change that is none reads as an empty name.
        subscription.ScheduleChangeOf(StateOf(to), tenantClosed: false).ToString().ShouldBe(expected);
    }

    [Fact]
    public void ScheduleChangeOf_ADifferentScheduledPlan_IsANewMove()
    {
        var subscription = Subscription.Start(Scheduled, At);

        subscription.ScheduleChangeOf(Scheduled with { ScheduledPlanId = Free }, tenantClosed: false).ShouldBe(ScheduleChange.MoveScheduled);
    }

    // At the period's end the schedule moves the subscription to its plan: a plan change, which records tenant.plan_changed, not an undo.
    [Fact]
    public void ScheduleChangeOf_TheScheduledMoveApplied_IsNoScheduleChange()
    {
        var subscription = Subscription.Start(Scheduled, At);

        subscription.ScheduleChangeOf(Active with { PlanId = Team, CurrentPeriodEnd = At.AddMonths(2) }, tenantClosed: false).ShouldBeNull();
    }

    // A move at once drops a member's cancellation: the plan change records it.
    [Fact]
    public void ScheduleChangeOf_AMoveAtOnceAfterACancellation_IsNoScheduleChange()
    {
        var subscription = Subscription.Start(Cancelled, At);

        subscription.ScheduleChangeOf(Active with { PlanId = Team }, tenantClosed: false).ShouldBeNull();
    }

    [Fact]
    public void ScheduleChangeOf_TheEndOfACancelledSubscription_IsNoScheduleChange()
    {
        var subscription = Subscription.Start(Cancelled, At);

        subscription.ScheduleChangeOf(Cancelled with { Status = SubscriptionStatus.Canceled }, tenantClosed: false).ShouldBeNull();
    }

    // B8: the closure's end and the reopen's undo are the platform's, which tenant.closed and tenant.reopened record.
    [Fact]
    public void ScheduleChangeOf_TheClosuresEnd_IsNoScheduleChange()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.EndForClosure();

        subscription.ScheduleChangeOf(Cancelled, tenantClosed: true).ShouldBeNull();
    }

    [Fact]
    public void ScheduleChangeOf_TheReopensUndo_IsNoScheduleChange()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.EndForClosure();
        subscription.Sync(Cancelled, At.AddMinutes(1));
        subscription.KeepAfterReopen();

        subscription.ScheduleChangeOf(Active, tenantClosed: false).ShouldBeNull();
    }

    [Fact]
    public void ScheduleChangeOf_AnotherSubscription_IsNoScheduleChange()
    {
        var subscription = Subscription.Start(Active, At);

        subscription.ScheduleChangeOf(Cancelled with { SubscriptionId = "sub_2" }, tenantClosed: false).ShouldBeNull();
    }

    private static SubscriptionState StateOf(string name) => name switch
    {
        nameof(Active) => Active,
        nameof(Cancelled) => Cancelled,
        _ => Scheduled,
    };
}
