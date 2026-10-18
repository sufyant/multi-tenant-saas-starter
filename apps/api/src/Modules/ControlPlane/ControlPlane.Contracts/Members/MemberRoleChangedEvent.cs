namespace ControlPlane.Contracts.Members;

/// <summary>
/// An Owner or an Admin gave a member another built-in role, or a member changed their own. Published through the outbox only when the
/// role changed; a subscriber handles it in the tenant's transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The catalog user id of the member whose role changed.</param>
/// <param name="ChangedBy">The catalog user id of the member who changed it, who acted.</param>
/// <param name="OldRole">The built-in role the member had: <c>owner</c>, <c>admin</c> or <c>member</c>.</param>
/// <param name="NewRole">The built-in role the member has now.</param>
public sealed record MemberRoleChangedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid UserId,
    Guid ChangedBy,
    string OldRole,
    string NewRole);
