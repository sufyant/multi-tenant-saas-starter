using SharedKernel;

namespace ControlPlane.Application.SystemAdmins.RevokeSystemAdmin;

/// <summary>
/// A system admin takes the grant away from another system admin who is not a root (docs/flows/system-admins.md). The user stays,
/// with every membership. It runs without a tenant.
/// </summary>
[MessageName("revoke-system-admin")]
public sealed record RevokeSystemAdminCommand(string ActorId, Guid UserId);
