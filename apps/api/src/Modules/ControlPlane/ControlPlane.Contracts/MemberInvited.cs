namespace ControlPlane.Contracts;

/// <summary>
/// An Owner or an Admin invited someone to the tenant, with a role. Published through the outbox; Audit records it, in its own
/// transaction, as the tenant's id travels in the message's envelope. The email goes out later, once the identity provider has let the
/// address sign up (<see cref="MemberInvitationReady"/>).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="InvitedBy">The catalog user id of the member who invited, who acted.</param>
/// <param name="Role">The built-in role the invitation gives: <c>owner</c>, <c>admin</c> or <c>member</c>. A message written before step 20k names it as the domain does: <c>Owner</c>.</param>
/// <param name="ReplacedInvitationId">The pending invitation to the same address that this one cancelled, if there was one (K2).</param>
public sealed record MemberInvited(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid InvitationId,
    Guid InvitedBy,
    string Role,
    Guid? ReplacedInvitationId);
