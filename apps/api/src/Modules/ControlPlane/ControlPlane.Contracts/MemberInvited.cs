namespace ControlPlane.Contracts;

/// <summary>
/// An Owner or an Admin invited someone to the tenant, with a role. Published through the outbox; Audit records it and Notifications
/// emails the link, each in its own transaction, as the tenant's id travels in the message's envelope.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="InvitedBy">The catalog user id of the member who invited, who acted.</param>
/// <param name="Role">The built-in role the invitation gives: Owner, Admin or Member.</param>
/// <param name="ReplacedInvitationId">The pending invitation to the same address that this one cancelled, if there was one (K2).</param>
/// <param name="Email">The invited address, for Notifications only. Audit stores no email address (K3).</param>
/// <param name="Link">
/// Where the invited person accepts. It carries the invitation's secret, which cannot be produced again later; see the deviations list
/// in ARCHITECTURE.md.
/// </param>
public sealed record MemberInvited(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid InvitationId,
    Guid InvitedBy,
    string Role,
    Guid? ReplacedInvitationId,
    string Email,
    string TenantName,
    Uri Link);
