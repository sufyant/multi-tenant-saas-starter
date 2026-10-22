using SharedKernel;

namespace ControlPlane.Contracts.Members;

/// <summary>
/// An Owner or an Admin invited someone to the tenant, with a role. Published through the outbox; Audit records it, in its own
/// transaction, as the tenant's id travels in the message's envelope. The email goes out later, once the identity provider has let the
/// address sign up (<see cref="Invitations.InvitationReadyEvent"/>).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="InvitedBy">The user id of the member who invited, who acted.</param>
/// <param name="Role">The name of the role the invitation gives: <c>owner</c>, <c>admin</c>, <c>member</c> or a custom role's.</param>
/// <param name="ReplacedInvitationId">The pending invitation to the same address that this one cancelled, if there was one (K2).</param>
/// <param name="RoleId">The id of that role. Null only in a message that a version before step r4b published.</param>
/// <param name="RoleBuiltIn">Whether that role is built in. Null only in a message that a version before step r4b published.</param>
[MessageName("member-invited")]
public sealed record MemberInvitedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid InvitationId,
    Guid InvitedBy,
    string Role,
    Guid? ReplacedInvitationId,
    Guid? RoleId,
    bool? RoleBuiltIn);
