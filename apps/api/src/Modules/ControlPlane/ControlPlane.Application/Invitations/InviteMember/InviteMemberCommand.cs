using SharedKernel;

namespace ControlPlane.Application.Invitations.InviteMember;

/// <summary>
/// An Owner or an Admin invites someone to the active tenant by email, with a built-in role (<c>Owner</c>, <c>Admin</c>, <c>Member</c>),
/// in a language for the invitation's email: without one, the inviter's own. The host's idempotency layer protects the HTTP request (I1);
/// the command carries no request key, and the server chooses the invitation's id.
/// </summary>
public sealed record InviteMemberCommand(string ActorId, string Email, string Role, Language? Language);
