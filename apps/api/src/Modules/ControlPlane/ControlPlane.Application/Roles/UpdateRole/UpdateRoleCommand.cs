using SharedKernel;

namespace ControlPlane.Application.Roles.UpdateRole;

/// <summary>A member gives a custom role of the active tenant a name and exactly these permissions (A3).</summary>
[MessageName("update-role")]
public sealed record UpdateRoleCommand(string ActorId, Guid RoleId, string Name, IReadOnlyList<string> Permissions);
