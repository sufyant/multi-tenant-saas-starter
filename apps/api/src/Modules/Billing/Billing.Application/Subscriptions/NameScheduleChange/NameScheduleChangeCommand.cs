using SharedKernel;

namespace Billing.Application.Subscriptions.NameScheduleChange;

/// <summary>
/// The second step of a member's change of the period's end (B9): it names the tenant and the plans through ControlPlane's contracts, on
/// ControlPlane's DbContext, as one DbContext owns a handler's transaction (W1). <c>ApplySubscriptionHandler</c> sends it in the tenant's
/// transaction, with the moment and the event's id, so a copy of it publishes the same event (I1).
/// </summary>
/// <param name="Change">The name of the change, a <c>ScheduleChange</c>, as the interval and the status of <c>ApplySubscriptionCommand</c> travel.</param>
/// <param name="NewPlanId">The plan of a scheduled move; none for a cancellation, whose plan is the default plan, and for an undo.</param>
/// <param name="AppliesAt">The end of the paid period, for a cancellation and a move; none for an undo.</param>
[MessageName("name-schedule-change")]
public sealed record NameScheduleChangeCommand(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string Change,
    Guid ChangedBy,
    Guid PlanId,
    Guid? NewPlanId,
    DateTimeOffset? AppliesAt);
