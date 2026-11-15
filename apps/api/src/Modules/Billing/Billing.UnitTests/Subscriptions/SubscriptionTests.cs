using Billing.Domain.Subscriptions;
using SharedKernel;

namespace Billing.UnitTests.Subscriptions;

// A subscription takes the provider's state: the state it has changes nothing (I1), and only an active one pays for its plan.
public sealed class SubscriptionTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly SubscriptionState Active =
        new("cus_1", "sub_1", Guid.CreateVersion7(), BillingInterval.Monthly, SubscriptionStatus.Active, At.AddMonths(1), CancelAtPeriodEnd: false);

    [Fact]
    public void Sync_TheStateItHas_ChangesNothing()
    {
        var subscription = Subscription.Start(Active, At);

        var change = subscription.Sync(Active, At.AddMinutes(1));

        (change, subscription.UpdatedAt).ShouldBe((StateChange.Unchanged, At));
    }

    [Fact]
    public void Sync_AnotherState_TakesIt()
    {
        var subscription = Subscription.Start(Active, At);

        var change = subscription.Sync(Active with { Status = SubscriptionStatus.PastDue }, At.AddMinutes(1));

        (change, subscription.Status, subscription.PaysForItsPlan, subscription.UpdatedAt).ShouldBe((StateChange.Changed, SubscriptionStatus.PastDue, false, At.AddMinutes(1)));
    }

    // B5: subscription A ended and B replaced it; a late read of A, which Stripe may send after B's, keeps B.
    [Fact]
    public void Sync_ALateReadOfAnEndedSubscriptionAfterItsSuccessor_KeepsTheSuccessor()
    {
        var subscription = Subscription.Start(Active with { SubscriptionId = "sub_b" }, At);

        var change = subscription.Sync(Active with { SubscriptionId = "sub_a", Status = SubscriptionStatus.Canceled }, At.AddMinutes(1));

        (change, subscription.SubscriptionId, subscription.Status).ShouldBe((StateChange.Unchanged, "sub_b", SubscriptionStatus.Active));
    }

    // A new subscription after an ended one takes the row.
    [Fact]
    public void Sync_ANewSubscriptionAfterAnEndedOne_TakesTheRow()
    {
        var subscription = Subscription.Start(Active with { SubscriptionId = "sub_a", Status = SubscriptionStatus.Canceled }, At);

        var change = subscription.Sync(Active with { SubscriptionId = "sub_b" }, At.AddMinutes(1));

        (change, subscription.SubscriptionId, subscription.Status).ShouldBe((StateChange.Changed, "sub_b", SubscriptionStatus.Active));
    }

    // B8: the closure's end, then the reopen's undo; a late "end" that reached Stripe after the undo is asked to undo again at the next event.
    [Fact]
    public void ClosureCorrection_ALateEndAfterTheReopen_AsksForTheUndoAgainUntilTheStateShowsIt()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.EndForClosure();
        subscription.KeepAfterReopen();
        subscription.Sync(Active with { CancelAtPeriodEnd = true }, At.AddMinutes(1));

        var again = subscription.ClosureCorrection(tenantClosed: false);
        subscription.Sync(Active, At.AddMinutes(2));
        var done = subscription.ClosureCorrection(tenantClosed: false);

        (again, done, subscription.ClosureEnd).ShouldBe((false, null, ClosureEnd.None));
    }

    [Fact]
    public void ClosureCorrection_TheClosuresEndNotYetAtStripe_AsksForItAgain()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.EndForClosure();

        var again = subscription.ClosureCorrection(tenantClosed: true);

        again.ShouldBe(true);
    }

    // A subscription that arrives after the closure ends at its period's end too.
    [Fact]
    public void ClosureCorrection_ASubscriptionOfAClosedTenant_AsksForTheEnd()
    {
        var subscription = Subscription.Start(Active, At);

        var end = subscription.ClosureCorrection(tenantClosed: true);

        (end, subscription.ClosureEnd).ShouldBe((true, ClosureEnd.Asked));
    }

    // An end that a member asked for is not the closure's: the reopen keeps it.
    [Fact]
    public void KeepAfterReopen_AnEndTheClosureDidNotAskFor_AsksNothing()
    {
        var subscription = Subscription.Start(Active with { CancelAtPeriodEnd = true }, At);
        subscription.EndForClosure();

        var undo = subscription.KeepAfterReopen();

        (undo, subscription.ClosureEnd).ShouldBe((null, ClosureEnd.None));
    }

    // Another subscription starts with nothing asked of it.
    [Fact]
    public void Sync_AnotherSubscription_ForgetsWhatTheClosureAsked()
    {
        var subscription = Subscription.Start(Active with { SubscriptionId = "sub_a", Status = SubscriptionStatus.Canceled }, At);
        subscription.EndForClosure();
        subscription.Sync(Active with { SubscriptionId = "sub_a" }, At);
        subscription.EndForClosure();

        subscription.Sync(Active with { SubscriptionId = "sub_b" }, At.AddMinutes(1));

        subscription.ClosureEnd.ShouldBe(ClosureEnd.None);
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Trialing", false)]
    [InlineData("Incomplete", false)]
    [InlineData("Unpaid", false)]
    [InlineData("Canceled", false)]
    public void PaysForItsPlan_EachStatus_OnlyWhileActive(string status, bool pays)
    {
        var subscription = Subscription.Start(Active with { Status = Enum.Parse<SubscriptionStatus>(status) }, At);

        subscription.PaysForItsPlan.ShouldBe(pays);
    }

    // B8, B9: a member's own end is no closure's, so a late state of the reopen's undo never takes the member's cancellation back.

    [Fact]
    public void EndByMember_AfterAReopenThatStripeHasNotShownYet_AsksNothingAgainForTheMembersEnd()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.EndForClosure();
        subscription.Sync(Active with { CancelAtPeriodEnd = true }, At.AddMinutes(1));
        subscription.KeepAfterReopen();

        subscription.EndByMember();
        subscription.Sync(Active with { CancelAtPeriodEnd = true }, At.AddMinutes(2));

        (subscription.ClosureEnd, subscription.ClosureCorrection(tenantClosed: false)).ShouldBe((ClosureEnd.None, null));
    }

    [Fact]
    public void Sync_AScheduledMove_KeepsItsPlanAndInterval()
    {
        var subscription = Subscription.Start(Active, At);
        var team = Guid.CreateVersion7();

        var change = subscription.Sync(Active with { ScheduledPlanId = team, ScheduledInterval = BillingInterval.Monthly }, At.AddMinutes(1));

        (change, subscription.ScheduledPlanId, subscription.ScheduledInterval).ShouldBe((SharedKernel.StateChange.Changed, team, BillingInterval.Monthly));
    }
}
