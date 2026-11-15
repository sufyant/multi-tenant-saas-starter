namespace Billing.Domain.Subscriptions;

/// <summary>
/// When a member's move of the tenant's subscription to another plan or interval applies (B9; <c>docs/roadmap.md</c>, step 40b): an
/// upgrade and a move from monthly to yearly at once, with proration; a move to a lower plan and from yearly to monthly at the end of the
/// paid period; a move to the default plan is the cancellation, at the end of the paid period.
/// </summary>
internal enum PlanMove
{
    Now,
    AtPeriodEnd,
    Cancel,
}
