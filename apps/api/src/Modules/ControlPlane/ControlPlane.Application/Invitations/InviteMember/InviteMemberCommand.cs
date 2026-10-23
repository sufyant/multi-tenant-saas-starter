using SharedKernel;

namespace ControlPlane.Application.Invitations.InviteMember;

/// <summary>
/// A member invites someone to the active tenant by email, with one of the tenant's roles, by its id, in a language for the invitation's
/// email: without one, the inviter's own. The host's idempotency layer protects the HTTP request (I1); the command carries no request key,
/// and the server chooses the invitation's id.
/// </summary>
[MessageName("invite-member")]
public sealed record InviteMemberCommand(string ActorId, string Email, Guid RoleId, Language? Language);
