namespace ControlPlane.Contracts.Invitations;

/// <summary>
/// An Owner or an Admin revoked a pending invitation; its link no longer works. Published through the outbox; a subscriber handles it in
/// the tenant's transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="RevokedBy">The catalog user id of the member who revoked it, who acted.</param>
public sealed record InvitationRevokedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid TenantId, Guid InvitationId, Guid RevokedBy);
