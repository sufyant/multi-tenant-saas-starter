namespace ControlPlane.Contracts;

/// <summary>
/// A system admin invited an Owner into a tenant that has none, because its first Owner never accepted. Published through the outbox;
/// Audit records it in the platform's log and in the tenant's, as the tenant's id travels in the message's envelope (K3). The email goes
/// out later, once the identity provider has let the address sign up (<see cref="MemberInvitationReadyEvent"/>).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="InvitedBy">The catalog user id of the system admin who invited, who acted.</param>
/// <param name="TenantName">The tenant's name at that moment, which the records keep.</param>
/// <param name="ReplacedInvitationId">The pending Owner invitation that this one cancelled, if there was one.</param>
public sealed record OwnerInvitedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid InvitationId,
    Guid InvitedBy,
    Guid? ReplacedInvitationId);
