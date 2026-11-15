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

    /// <summary>
    /// What our app asked the provider about the end of the subscription because of the tenant's closure (B8), so a reopen takes back only
    /// that end, never one that a member asked for.
    /// </summary>
    public ClosureEnd ClosureEnd { get; private set; }

    /// <summary>When billing last wrote the row.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Whether the subscription puts the tenant on its plan: only while it is active (Stripe, "Subscription statuses").</summary>
    public bool PaysForItsPlan => Status == SubscriptionStatus.Active;

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
            && state.Status == Status && state.CurrentPeriodEnd == CurrentPeriodEnd && state.CancelAtPeriodEnd == CancelAtPeriodEnd)
        {
            return StateChange.Unchanged;
        }

        // Another subscription starts with nothing asked of it (B8).
        if (state.SubscriptionId != SubscriptionId)
        {
            ClosureEnd = ClosureEnd.None;
        }

        (CustomerId, SubscriptionId, PlanId, Interval, Status, CurrentPeriodEnd, CancelAtPeriodEnd, UpdatedAt) =
            (state.CustomerId, state.SubscriptionId, state.PlanId, state.Interval, state.Status, state.CurrentPeriodEnd, state.CancelAtPeriodEnd, at);
        return StateChange.Changed;
    }
}
