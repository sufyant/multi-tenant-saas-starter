using Billing.Application.Provider;
using SharedKernel;

namespace Billing.Application.Subscriptions.SchedulePlanChange;

/// <summary>
/// Moves the subscription to the order at the end of the period it has paid for (B9). The provider's event of the change then brings the
/// scheduled move to the tenant's row.
/// </summary>
[MessageName("schedule-plan-change")]
public sealed record SchedulePlanChangeCommand(PlanChangeOrder Order);
