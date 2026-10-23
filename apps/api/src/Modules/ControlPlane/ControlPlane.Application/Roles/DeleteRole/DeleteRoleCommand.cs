using SharedKernel;

namespace ControlPlane.Application.Roles.DeleteRole;

/// <summary>A member deletes a custom role of the active tenant that no membership and no pending invitation uses (A3).</summary>
[MessageName("delete-role")]
public sealed record DeleteRoleCommand(string ActorId, Guid RoleId);
