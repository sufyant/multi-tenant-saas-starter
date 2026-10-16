using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// A user's membership in a tenant, with the role it is assigned; authorization comes from here, not from the user record.
/// </summary>
internal sealed class Membership(Guid tenantId, Guid userId, Guid roleId) : ITenantEntity
{
    public Guid TenantId { get; private init; } = tenantId;

    public Guid UserId { get; private init; } = userId;

    public Guid RoleId { get; private set; } = roleId;

    /// <summary>Gives the membership the role. Only <see cref="Tenant.ChangeMemberRole"/> calls it, after the tenant's rules.</summary>
    internal bool Assign(Role role)
    {
        if (RoleId == role.Id)
        {
            return false;
        }

        RoleId = role.Id;
        return true;
    }
}
