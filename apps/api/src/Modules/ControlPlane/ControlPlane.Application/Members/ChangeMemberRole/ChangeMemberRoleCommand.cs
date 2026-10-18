using SharedKernel;

namespace ControlPlane.Application.Members.ChangeMemberRole;

/// <summary>
/// An Owner or an Admin gives a member of the active tenant a built-in role (<c>Owner</c>, <c>Admin</c>, <c>Member</c>). A member may
/// change their own role under the same rules (K1).
/// </summary>
[MessageName("change-member-role")]
public sealed record ChangeMemberRoleCommand(string ActorId, Guid UserId, string Role);
