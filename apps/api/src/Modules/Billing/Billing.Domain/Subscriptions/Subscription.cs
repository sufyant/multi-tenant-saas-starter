using SharedKernel;

namespace Billing.Domain.Subscriptions;

/// <summary>
/// A tenant's subscription at the billing provider, as billing last read it: one row for each tenant that paid by card once (step 40). The
/// provider keeps the money; this row keeps what our app decides with: the plan it pays for, its state and its period. Each change comes
/// from the provider's own state, read after a signed webhook, never from the webhook's body alone (Stripe, "Webhooks", "Event ordering").
/// </summary>
internal sealed class Subscription : ITenantEntity
{
    private Subscription(string customerId, string subscriptionId)
    {
        CustomerId = customerId;
        SubscriptionId = subscriptionId;
    }

    /// <summary>The provider's id of the tenant's customer, which holds its card, address and invoices.</summary>
    public string CustomerId { get; private set; }

    /// <summary>The provider's id of the subscription.</summary>
    public string SubscriptionId { get; private set; }

    /// <summary>The plan the subscription pays for.</summary>
    public Guid PlanId { get; private set; }

    public BillingInterval Interval { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    /// <summary>The end of the period the subscription has paid for.</summary>
    public DateTimeOffset CurrentPeriodEnd { get; private set; }

    /// <summary>Whether the subscription ends at <see cref="CurrentPeriodEnd"/>.</summary>
    public bool CancelAtPeriodEnd { get; private set; }

    /// <summary>The plan the subscription moves to at <see cref="CurrentPeriodEnd"/>, by a member's wish (B9); none when nothing is scheduled.</summary>
    public Guid? ScheduledPlanId { get; private set; }

    /// <summary>How often the subscription pays after the scheduled move; none when nothing is scheduled.</summary>
    public BillingInterval? ScheduledInterval { get; private set; }

    /// <summary>
    /// What our app asked the provider about the end of the subscription because of the tenant's closure (B8), so a reopen takes back only
    /// that end, never one that a member asked for.
    /// </summary>
    public ClosureEnd ClosureEnd { get; private set; }

    /// <summary>The open invoice whose payment failed (B11); none when nothing is due.</summary>
    public FailedPayment? FailedPayment { get; private set; }

    /// <summary>When billing's grace ends, after the provider's retries ended (B11); none while nothing is unpaid after them.</summary>
    public DateTimeOffset? GraceEndsAt { get; private set; }

    /// <summary>When the grace ended and the tenant moved to the default plan (B11); none until then, and again once the subscription pays.</summary>
    public DateTimeOffset? MovedToDefaultAt { get; private set; }

    /// <summary>When billing last wrote the row.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Whether the subscription puts the tenant on its plan: only while it is active (Stripe, "Subscription statuses").</summary>
    public bool PaysForItsPlan => Status == SubscriptionStatus.Active;

    /// <summary>Where a failed payment stands (B11; 10.15 to 10.17); none when nothing is due.</summary>
    public PaymentState? PaymentState =>
        MovedToDefaultAt is not null ? Subscriptions.PaymentState.MovedToDefault
        : GraceEndsAt is not null ? Subscriptions.PaymentState.Grace
        : FailedPayment is not null && Status is SubscriptionStatus.PastDue or SubscriptionStatus.Unpaid ? Subscriptions.PaymentState.Retrying
        : null;

    /// <summary>Whether a subscription in this state has ended, so the tenant may pay again (Stripe, "Subscription statuses").</summary>
    public static bool HasEnded(SubscriptionStatus status) => status is SubscriptionStatus.Canceled or SubscriptionStatus.IncompleteExpired;

    /// <summary>A tenant's first subscription, from the provider's state.</summary>
    public static Subscription Start(SubscriptionState state, DateTimeOffset at)
    {
        var subscription = new Subscription(state.CustomerId, state.SubscriptionId);
        subscription.Sync(state, at);
        return subscription;
    }

    /// <summary>
    /// The end at the period's end that the tenant's closure asks for (B8): true to ask for it, none when nothing is to ask, because the
    /// subscription has ended or ends at its period's end already, by a member's wish too.
    /// </summary>
    public bool? EndForClosure()
    {
        if (HasEnded(Status) || CancelAtPeriodEnd)
        {
            return null;
        }

        ClosureEnd = ClosureEnd.Asked;
        return true;
    }

    /// <summary>The undo of the closure's end that the reopen asks for: false to ask for it, none when the closure asked for no end.</summary>
    public bool? KeepAfterReopen()
    {
        if (ClosureEnd != ClosureEnd.Asked || HasEnded(Status))
        {
            ClosureEnd = ClosureEnd.None;
            return null;
        }

        ClosureEnd = ClosureEnd.Released;
        return false;
    }

    /// <summary>
    /// A member's own end of the subscription at the period's end, or its undo (B9): the closure asked for nothing of it, so a reopen never
    /// takes it back and a late state never asks the closure's wish again.
    /// </summary>
    public void EndByMember() => ClosureEnd = ClosureEnd.None;

    /// <summary>
    /// What to ask the provider again after its state changed, so it ends in what we asked whatever order our calls reached it in (B8):
    /// the end again while the closure asks for it and the state has none; the undo again while the reopen asks for it and the state ends
    /// at the period's end; and the end for a subscription that arrived after the tenant's closure. An undo that the state shows is done.
    /// </summary>
    public bool? ClosureCorrection(bool tenantClosed)
    {
        if (HasEnded(Status))
        {
            return null;
        }

        switch (ClosureEnd, CancelAtPeriodEnd)
        {
            case (ClosureEnd.Asked, false):
                return true;
            case (ClosureEnd.Released, true):
                return false;
            case (ClosureEnd.Released, false):
                ClosureEnd = ClosureEnd.None;
                return null;
            case (ClosureEnd.None, false) when tenantClosed:
                return EndForClosure();
            default:
                return null;
        }
    }

    /// <summary>
    /// The member's change of the period's end that the provider's new state shows, before the row takes it (B9): none when the state
    /// changes nothing at the period's end, moves the plan or the interval now, is another subscription's, or comes while the tenant is
    /// closed or the closure or the reopen asked the provider about the end (B8), as that end is the closure's, not a member's. A move
    /// scheduled after a cancellation replaces it, so it is a move; a cancellation drops a scheduled move, so it is a cancellation.
    /// </summary>
    public ScheduleChange? ScheduleChangeOf(SubscriptionState state, bool tenantClosed)
    {
        if (tenantClosed || ClosureEnd != ClosureEnd.None || state.SubscriptionId != SubscriptionId || !PaysForItsPlan
            || state.Status != SubscriptionStatus.Active || state.PlanId != PlanId || state.Interval != Interval)
        {
            return null;
        }

        if (state.CancelAtPeriodEnd)
        {
            return CancelAtPeriodEnd ? null : ScheduleChange.Cancelled;
        }

        if (state.ScheduledPlanId is not null)
        {
            return (state.ScheduledPlanId, state.ScheduledInterval) == (ScheduledPlanId, ScheduledInterval) ? null : ScheduleChange.MoveScheduled;
        }

        return CancelAtPeriodEnd ? ScheduleChange.CancellationUndone : ScheduledPlanId is null ? null : ScheduleChange.MoveUndone;
    }

    /// <summary>
    /// What the provider's new state asks of the dunning (B11), before the row takes it: a payment of this subscription that failed on an
    /// invoice the row has not seen fail, and the end of this subscription, which moves the tenant to the default plan. The end is a member's
    /// or a closure's when the subscription ended at its period's end, else it ended unpaid; an end after the grace's move is none, as the
    /// tenant is there already. Another subscription's state is none of this one's (B5).
    /// </summary>
    public Dunning DunningOf(SubscriptionState state)
    {
        if (state.SubscriptionId != SubscriptionId)
        {
            return new Dunning(PaymentFailed: false, End: null);
        }

        var failed = state.Status is SubscriptionStatus.PastDue or SubscriptionStatus.Unpaid && state.Payment is { } payment
            && payment.InvoiceId != FailedPayment?.InvoiceId;
        PlanEnd? end = state.Status == SubscriptionStatus.Canceled && !HasEnded(Status) && MovedToDefaultAt is null
            ? CancelAtPeriodEnd ? PlanEnd.Ended : PlanEnd.Unpaid
            : null;
        return new Dunning(failed, end);
    }

    /// <summary>
    /// Starts billing's grace once the provider's retries of an unpaid invoice are over (B11): the grace's end, which the caller schedules, or
    /// none when it runs already or nothing is to start. Only a subscription that is past due or unpaid has a grace: a first payment that
    /// failed at Checkout leaves it incomplete, and the provider ends it.
    /// </summary>
    public DateTimeOffset? StartGrace(DateTimeOffset now, TimeSpan grace)
    {
        if (GraceEndsAt is not null || FailedPayment is not { NextAttemptAt: null } || Status is not (SubscriptionStatus.PastDue or SubscriptionStatus.Unpaid))
        {
            return null;
        }

        GraceEndsAt = now + grace;
        return GraceEndsAt;
    }

    /// <summary>
    /// Ends the grace that ends at this moment, when it is over and the subscription still does not pay (B11): true once, when the tenant
    /// moves to the default plan. A grace that a payment ended, or that another grace replaced, ends nothing.
    /// </summary>
    public bool EndGrace(DateTimeOffset graceEndsAt, DateTimeOffset now)
    {
        if (GraceEndsAt != graceEndsAt || now < graceEndsAt || MovedToDefaultAt is not null || PaysForItsPlan || HasEnded(Status))
        {
            return false;
        }

        MovedToDefaultAt = now;
        return true;
    }

    /// <summary>
    /// Takes the provider's state. A state it has already changes nothing (I1). Another subscription of the customer replaces this one
    /// unless the other has ended: a late read of an ended subscription never takes the row back from the one that replaced it, as the
    /// provider sends its events in no order (B5).
    /// </summary>
    public StateChange Sync(SubscriptionState state, DateTimeOffset at)
    {
        if (state.SubscriptionId != SubscriptionId && HasEnded(state.Status))
        {
            return StateChange.Unchanged;
        }

        if (state.CustomerId == CustomerId && state.SubscriptionId == SubscriptionId && state.PlanId == PlanId && state.Interval == Interval
            && state.Status == Status && state.CurrentPeriodEnd == CurrentPeriodEnd && state.CancelAtPeriodEnd == CancelAtPeriodEnd
            && state.ScheduledPlanId == ScheduledPlanId && state.ScheduledInterval == ScheduledInterval && state.Payment == FailedPayment)
        {
            return StateChange.Unchanged;
        }

        // Another subscription starts with nothing asked of it (B8), and with nothing unpaid (B11).
        if (state.SubscriptionId != SubscriptionId)
        {
            ClosureEnd = ClosureEnd.None;
            (GraceEndsAt, MovedToDefaultAt) = (null, null);
        }

        // B11: a paid invoice ends the grace, and a subscription that pays again takes the tenant back from the default plan.
        FailedPayment = state.Payment;
        if (state.Payment is null)
        {
            GraceEndsAt = null;
        }

        if (state.Status == SubscriptionStatus.Active && state.Payment is null)
        {
            MovedToDefaultAt = null;
        }

        (CustomerId, SubscriptionId, PlanId, Interval, Status, CurrentPeriodEnd, CancelAtPeriodEnd, UpdatedAt) =
            (state.CustomerId, state.SubscriptionId, state.PlanId, state.Interval, state.Status, state.CurrentPeriodEnd, state.CancelAtPeriodEnd, at);
        (ScheduledPlanId, ScheduledInterval) = (state.ScheduledPlanId, state.ScheduledInterval);
        return StateChange.Changed;
    }
}
