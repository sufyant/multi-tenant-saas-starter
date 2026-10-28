using SharedKernel;

namespace ControlPlane.Contracts.Plans;

/// <summary>
/// A system admin put a tenant on another plan. Published through the outbox; a subscriber handles it in the tenant's transaction, as the
/// tenant's id travels in the message's envelope. It names both plans as they were at that moment.
/// </summary>
/// <param name="ChangedBy">The user id of the system admin who changed the plan.</param>
[MessageName("tenant-plan-changed")]
public sealed record TenantPlanChangedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid ChangedBy,
    Guid OldPlanId,
    string OldPlanName,
    Guid NewPlanId,
    string NewPlanName);
