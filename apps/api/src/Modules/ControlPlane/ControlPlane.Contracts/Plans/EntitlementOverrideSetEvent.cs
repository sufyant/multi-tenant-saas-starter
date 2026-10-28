using SharedKernel;

namespace ControlPlane.Contracts.Plans;

/// <summary>
/// A system admin set a tenant's override of one key, new or changed. Published only when something changed. Only the platform's log
/// keeps it: the reason is the provider's own note, which a member never reads.
/// </summary>
/// <param name="SetBy">The user id of the system admin who set it.</param>
/// <param name="OldValue">The override's value before, none for a new one.</param>
[MessageName("entitlement-override-set")]
public sealed record EntitlementOverrideSetEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid SetBy,
    string Key,
    EntitlementValueRecord? OldValue,
    DateTimeOffset? OldExpiresAt,
    EntitlementValueRecord NewValue,
    DateTimeOffset? NewExpiresAt,
    string Reason);
