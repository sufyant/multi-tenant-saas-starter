using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// A user's membership in a tenant, with the role it is assigned; authorization comes from here, not from the user record.
/// </summary>
internal sealed class Membership(Guid tenantId, Guid userId, Guid roleId, DateTimeOffset joinedAt) : ITenantEntity
{
    /// <summary>The target of a change has no membership in the tenant; row level security hides another tenant's members.</summary>
    public static readonly Error NotFound = Error.NotFound(ControlPlaneErrorCodes.MemberNotFound, "The member was not found.");

    public Guid TenantId { get; private init; } = tenantId;

    public Guid UserId { get; private init; } = userId;

    public Guid RoleId { get; private set; } = roleId;

    /// <summary>When the user joined the tenant. The Owner who joined first is the one the platform's tenant list names.</summary>
    public DateTimeOffset JoinedAt { get; private init; } = joinedAt;

    /// <summary>Gives the membership the role. Only <see cref="Tenant.ChangeRole"/> calls it, after the tenant's rules.</summary>
    internal StateChange Assign(Role role)
    {
        if (RoleId == role.Id)
        {
            return StateChange.Unchanged;
        }

        RoleId = role.Id;
        return StateChange.Changed;
    }
}
