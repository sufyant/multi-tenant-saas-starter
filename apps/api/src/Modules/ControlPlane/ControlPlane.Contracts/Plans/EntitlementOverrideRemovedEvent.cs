using SharedKernel;

namespace ControlPlane.Contracts.Plans;

/// <summary>A system admin removed a tenant's override of one key: the plan's value applies again. Only the platform's log keeps it.</summary>
/// <param name="RemovedBy">The user id of the system admin who removed it.</param>
/// <param name="OldValue">The value the override had.</param>
[MessageName("entitlement-override-removed")]
public sealed record EntitlementOverrideRemovedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid RemovedBy,
    string Key,
    EntitlementValueRecord OldValue,
    DateTimeOffset? OldExpiresAt);
