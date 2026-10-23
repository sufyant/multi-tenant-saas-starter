using SharedKernel;

namespace ControlPlane.Contracts.Roles;

/// <summary>
/// A member changed the name or the permissions of a custom role of the tenant (A3). Published through the outbox only when something
/// changed; Audit records it in the tenant's log, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UpdatedBy">The user id of the member who changed the role, who acted.</param>
/// <param name="OldName">The name the role had.</param>
/// <param name="NewName">The name the role has now.</param>
/// <param name="PermissionsAdded">The permissions the role holds now and did not hold, in ordinal order.</param>
/// <param name="PermissionsRemoved">The permissions the role held and holds no more, in ordinal order.</param>
[MessageName("role-updated")]
public sealed record RoleUpdatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid RoleId,
    Guid UpdatedBy,
    string OldName,
    string NewName,
    IReadOnlyList<string> PermissionsAdded,
    IReadOnlyList<string> PermissionsRemoved);
