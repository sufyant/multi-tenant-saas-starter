namespace Billing.Domain.Subscriptions;

/// <summary>
/// The state of a subscription at the billing provider, as Stripe names it (Stripe, "Subscriptions", "Subscription statuses"). Only an
/// active subscription puts the tenant on its plan.
/// </summary>
internal enum SubscriptionStatus
{
    Incomplete,
    IncompleteExpired,
    Trialing,
    Active,
    PastDue,
    Canceled,
    Unpaid,
    Paused,
}
