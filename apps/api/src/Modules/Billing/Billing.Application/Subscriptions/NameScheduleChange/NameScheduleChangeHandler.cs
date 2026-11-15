using Billing.Contracts.Subscriptions;
using Billing.Domain.Subscriptions;
using ControlPlane.Contracts.Plans;
using ControlPlane.Contracts.Tenants;
using Wolverine;

namespace Billing.Application.Subscriptions.NameScheduleChange;

// The names of this moment, which the Activity log keeps (K3). A tenant that a purge deleted since, or a plan that is gone, leaves nothing
// to name, and nothing is recorded. The event goes through the outbox, in the tenant of the envelope (W3).
public static class NameScheduleChangeHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        NameScheduleChangeCommand command,
        IPlanDirectory plans,
        ITenantNames tenants,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindNameAsync(command.TenantId, cancellationToken) is not { } tenantName
            || await plans.FindPlanAsync(command.PlanId, cancellationToken) is not { } plan)
        {
            return [];
        }

        var (eventId, at, tenantId, by) = (command.EventId, command.OccurredAt, command.TenantId, command.ChangedBy);
        switch (Enum.Parse<ScheduleChange>(command.Change))
        {
            case ScheduleChange.Cancelled when command.AppliesAt is { } movesAt
                && await plans.FindDefaultPlanAsync(cancellationToken) is { } defaultPlan:
                return [new PlanCancelledEvent(eventId, at, tenantId, tenantName, by, plan.Id, plan.Name, defaultPlan.Id, defaultPlan.Name, movesAt)];
            case ScheduleChange.MoveScheduled when command.AppliesAt is { } appliesAt && command.NewPlanId is { } newPlanId
                && await plans.FindPlanAsync(newPlanId, cancellationToken) is { } newPlan:
                return [new PlanChangeScheduledEvent(eventId, at, tenantId, tenantName, by, plan.Id, plan.Name, newPlan.Id, newPlan.Name, appliesAt)];
            case ScheduleChange.CancellationUndone:
                return [new PlanCancellationUndoneEvent(eventId, at, tenantId, tenantName, by, plan.Id, plan.Name)];
            case ScheduleChange.MoveUndone:
                return [new PlanChangeUndoneEvent(eventId, at, tenantId, tenantName, by, plan.Id, plan.Name)];
            default:
                return [];
        }
    }
}
