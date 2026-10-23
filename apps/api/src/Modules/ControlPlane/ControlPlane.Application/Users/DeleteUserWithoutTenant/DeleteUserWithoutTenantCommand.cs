using SharedKernel;

namespace ControlPlane.Application.Users.DeleteUserWithoutTenant;

/// <summary>
/// A tenant's purge took the user's last membership away: the platform deletes the user, through the same steps as a system admin's
/// deletion (A8; section 6, "Tenant purge"). It runs without a tenant, with the user declared (R11).
/// </summary>
[MessageName("delete-user-without-tenant")]
public sealed record DeleteUserWithoutTenantCommand(Guid UserId);
