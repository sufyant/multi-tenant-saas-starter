namespace ControlPlane.Contracts.Members;

/// <summary>
/// A user accepted an invitation and became a member of the tenant. Published through the outbox; a subscriber handles it in the
/// tenant's transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The catalog user id of the new member, who acted.</param>
public sealed record MemberJoinedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid UserId);
