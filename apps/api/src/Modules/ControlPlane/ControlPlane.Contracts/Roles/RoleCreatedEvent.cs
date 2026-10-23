using SharedKernel;

namespace ControlPlane.Contracts.Roles;

/// <summary>
/// A member created a custom role of the tenant (A3). Published through the outbox; Audit records it in the tenant's log, in its own
/// transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="CreatedBy">The user id of the member who created the role, who acted.</param>
/// <param name="Name">The role's name.</param>
/// <param name="Permissions">The role's permissions, in ordinal order.</param>
[MessageName("role-created")]
public sealed record RoleCreatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid RoleId,
    Guid CreatedBy,
    string Name,
    IReadOnlyList<string> Permissions);
