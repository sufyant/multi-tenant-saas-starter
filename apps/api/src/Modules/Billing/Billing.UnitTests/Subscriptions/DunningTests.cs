using Billing.Domain.Subscriptions;

namespace Billing.UnitTests.Subscriptions;

// B11, the dunning: Stripe retries a failed payment, then billing's grace follows, then the tenant moves to the default plan. The row reads
// each step from the provider's state; a copy of a state starts nothing twice (I1).
public sealed class DunningTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Grace = TimeSpan.FromDays(7);

    private static readonly SubscriptionState Active =
        new("cus_1", "sub_1", Guid.CreateVersion7(), BillingInterval.Monthly, SubscriptionStatus.Active, At.AddMonths(1), CancelAtPeriodEnd: false);

    private static readonly FailedPayment Retrying = new("in_1", 10000, "EUR", At, 1, At.AddDays(3), "visa", "4242");

    private static readonly SubscriptionState PastDue = Active with { Status = SubscriptionStatus.PastDue, Payment = Retrying };

    private static readonly SubscriptionState RetriesOver = PastDue with { Payment = Retrying with { Attempts = 4, NextAttemptAt = null } };

    [Fact]
    public void DunningOf_AFirstFailedPayment_IsAFailure()
    {
        var subscription = Subscription.Start(Active, At);

        subscription.DunningOf(PastDue).ShouldBe(new Dunning(PaymentFailed: true, End: null));
    }

    [Fact]
    public void DunningOf_AnotherAttemptOfTheSameInvoice_IsNoNewFailure()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.Sync(PastDue, At);

        subscription.DunningOf(PastDue with { Payment = Retrying with { Attempts = 2 } }).ShouldBe(new Dunning(PaymentFailed: false, End: null));
    }

    [Fact]
    public void StartGrace_WhileStripeStillRetries_StartsNothing()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.Sync(PastDue, At);

        subscription.StartGrace(At.AddDays(1), Grace).ShouldBeNull();
    }

    [Fact]
    public void StartGrace_RetriesThatEnded_StartsTheGraceOnce()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.Sync(RetriesOver, At);

        var (first, second) = (subscription.StartGrace(At.AddDays(14), Grace), subscription.StartGrace(At.AddDays(15), Grace));

        (first, second, subscription.GraceEndsAt, subscription.PaymentState).ShouldBe((At.AddDays(21), null, At.AddDays(21), PaymentState.Grace));
    }

    // A first payment that failed at Checkout leaves the subscription incomplete, with no next attempt: Stripe ends it, and no grace runs.
    [Fact]
    public void StartGrace_AnIncompleteFirstPayment_StartsNothing()
    {
        var subscription = Subscription.Start(RetriesOver with { Status = SubscriptionStatus.Incomplete }, At);

        (subscription.StartGrace(At, Grace), subscription.PaymentState).ShouldBe((null, null));
    }

    [Fact]
    public void EndGrace_AtItsEnd_MovesToTheDefaultPlanOnce()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.Sync(RetriesOver, At);
        var graceEndsAt = subscription.StartGrace(At.AddDays(14), Grace)!.Value;

        var (first, second) = (subscription.EndGrace(graceEndsAt, graceEndsAt), subscription.EndGrace(graceEndsAt, graceEndsAt.AddMinutes(1)));

        (first, second, subscription.MovedToDefaultAt, subscription.PaymentState).ShouldBe((true, false, graceEndsAt, PaymentState.MovedToDefault));
    }

    [Fact]
    public void EndGrace_BeforeItsEnd_MovesNothing()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.Sync(RetriesOver, At);
        var graceEndsAt = subscription.StartGrace(At.AddDays(14), Grace)!.Value;

        subscription.EndGrace(graceEndsAt, graceEndsAt.AddMinutes(-1)).ShouldBeFalse();
    }

    // A message of a grace that a payment ended, then a new grace replaced, moves nothing: only the grace it names ends.
    [Fact]
    public void EndGrace_AfterThePayment_MovesNothing()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.Sync(RetriesOver, At);
        var graceEndsAt = subscription.StartGrace(At.AddDays(14), Grace)!.Value;
        subscription.Sync(Active, At.AddDays(16));

        (subscription.EndGrace(graceEndsAt, graceEndsAt), subscription.GraceEndsAt, subscription.PaymentState).ShouldBe((false, null, null));
    }

    [Fact]
    public void Sync_APaymentAfterTheMove_ClearsTheMove()
    {
        var subscription = Subscription.Start(Active, At);
        subscription.Sync(RetriesOver, At);
        var graceEndsAt = subscription.StartGrace(At.AddDays(14), Grace)!.Value;
        subscription.EndGrace(graceEndsAt, graceEndsAt);

        subscription.Sync(Active, At.AddDays(30));

        (subscription.MovedToDefaultAt, subscription.PaysForItsPlan, subscription.PaymentState).ShouldBe((null, true, null));
    }

    [Fact]
    public void PaymentState_WhileStripeRetries_IsRetrying()
    {
        var subscription = Subscription.Start(Active, At);

        subscription.Sync(PastDue, At);

        (subscription.PaymentState, subscription.FailedPayment).ShouldBe((PaymentState.Retrying, Retrying));
    }

    // The end of a subscription that a member cancelled moves the tenant to the default plan, by that member.
    [Fact]
    public void DunningOf_TheEndOfACancelledSubscription_IsAnEnd()
    {
        var subscription = Subscription.Start(Active with { CancelAtPeriodEnd = true }, At);

        subscription.DunningOf(Active with { Status = SubscriptionStatus.Canceled, CancelAtPeriodEnd = true }).End.ShouldBe(PlanEnd.Ended);
    }

    [Fact]
    public void DunningOf_AnUnpaidSubscriptionThatStripeEnds_IsAnUnpaidEnd()
    {
        var subscription = Subscription.Start(RetriesOver, At);

        subscription.DunningOf(RetriesOver with { Status = SubscriptionStatus.Canceled }).End.ShouldBe(PlanEnd.Unpaid);
    }

    [Fact]
    public void DunningOf_AnEndAfterTheMove_IsNoEnd()
    {
        var subscription = Subscription.Start(RetriesOver, At);
        var graceEndsAt = subscription.StartGrace(At.AddDays(14), Grace)!.Value;
        subscription.EndGrace(graceEndsAt, graceEndsAt);

        subscription.DunningOf(RetriesOver with { Status = SubscriptionStatus.Canceled }).End.ShouldBeNull();
    }

    // A late read of another subscription that ended is no end of this one (B5).
    [Fact]
    public void DunningOf_TheEndOfAnotherSubscription_IsNoEnd()
    {
        var subscription = Subscription.Start(Active, At);

        subscription.DunningOf(Active with { SubscriptionId = "sub_old", Status = SubscriptionStatus.Canceled }).End.ShouldBeNull();
    }
}
