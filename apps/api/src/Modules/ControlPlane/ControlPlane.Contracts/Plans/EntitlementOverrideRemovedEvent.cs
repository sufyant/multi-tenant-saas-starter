using SharedKernel;

namespace ControlPlane.Contracts.Plans;

/// <summary>A system admin removed a tenant's override of one key: the plan's value applies again. Only the platform's log keeps it.</summary>
/// <param name="RemovedBy">The user id of the system admin who removed it.</param>
/// <param name="OldValue">The value the override had.</param>
/// <param name="PlanValue">The plan's value of the key, which applies again, for a notification: all fields empty when the plan has none,
/// and the key is closed. None in an event of the version before step 33-backend-2.</param>
/// <param name="RetentionShortened">The Activity log period that got shorter with the removal, none when it did not (K14). None in an event
/// of the version before step 37c.</param>
[MessageName("entitlement-override-removed")]
public sealed record EntitlementOverrideRemovedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid RemovedBy,
    string Key,
    EntitlementValueRecord OldValue,
    DateTimeOffset? OldExpiresAt,
    EntitlementValueRecord? PlanValue = null,
    AuditRetentionShortening? RetentionShortened = null);
