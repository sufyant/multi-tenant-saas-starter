using SharedKernel;

namespace ControlPlane.Application.Members.ChangeMemberRole;

/// <summary>
/// A member gives a member of the active tenant one of the tenant's roles, by its id. A member may change their own role under the same
/// rules (K1).
/// </summary>
[MessageName("change-member-role")]
public sealed record ChangeMemberRoleCommand(string ActorId, Guid UserId, Guid RoleId);
