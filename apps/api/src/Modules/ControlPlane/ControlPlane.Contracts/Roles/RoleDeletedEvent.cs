using SharedKernel;

namespace ControlPlane.Contracts.Roles;

/// <summary>
/// A member deleted a custom role of the tenant that nobody used (A3). Published through the outbox; Audit records it in the tenant's
/// log, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="DeletedBy">The user id of the member who deleted the role, who acted.</param>
/// <param name="Name">The name the role had.</param>
[MessageName("role-deleted")]
public sealed record RoleDeletedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid RoleId, Guid DeletedBy, string Name);
