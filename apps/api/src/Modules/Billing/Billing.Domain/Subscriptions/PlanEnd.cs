namespace Billing.Domain.Subscriptions;

/// <summary>
/// Why the tenant leaves the plan its subscription paid for, for the default plan (B11): the subscription ended at the end of a period that a
/// member or a closure cancelled, or it ended unpaid.
/// </summary>
internal enum PlanEnd
{
    Ended,
    Unpaid,
}
