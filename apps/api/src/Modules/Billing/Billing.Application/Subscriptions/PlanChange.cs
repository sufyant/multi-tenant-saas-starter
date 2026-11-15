using Billing.Application.Provider;
using Billing.Domain.Subscriptions;

namespace Billing.Application.Subscriptions;

/// <summary>
/// A member's move of the tenant's subscription to a plan and an interval (B9): when it applies, the provider's order, and what the target
/// costs each interval for the seated members.
/// </summary>
/// <param name="Move">The name of a <c>PlanMove</c> (N7r): <c>Now</c>, <c>AtPeriodEnd</c> or <c>Cancel</c>.</param>
/// <param name="At">The end of the paid period, when a move that is not at once applies.</param>
/// <param name="PaidBy">The user id of the member who moves it.</param>
public sealed record PlanChange(string Move, PlanChangeOrder Order, long Amount, DateTimeOffset At, Guid PaidBy)
{
    public bool IsNow => Move == nameof(PlanMove.Now);

    public bool IsCancel => Move == nameof(PlanMove.Cancel);
}
