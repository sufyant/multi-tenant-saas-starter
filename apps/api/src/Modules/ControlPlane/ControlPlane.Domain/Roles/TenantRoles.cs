namespace ControlPlane.Domain.Roles;

/// <summary>
/// The roles of the active tenant, with their permissions, read without tracking, so a command finds the role of a membership or an
/// invitation, or the role it gives, and a rule asks what that role may do (A1). A command that changes a custom role reads that role
/// itself (<see cref="Tenants.RoleFacts"/>); every such command locks the tenant first (M1, K6a).
/// </summary>
internal sealed class TenantRoles(IReadOnlyList<Role> roles)
{
    /// <summary>The role with this id. Every membership and invitation of the tenant has one of the tenant's roles.</summary>
    public Role Of(Guid roleId) => roles.Single(role => role.Id == roleId);

    /// <summary>The role with this id, or none when the tenant has no such role: a request names a role by its id.</summary>
    public Role? Find(Guid roleId) => roles.SingleOrDefault(role => role.Id == roleId);

    /// <summary>The tenant's built-in role of this kind, for an assignment.</summary>
    public Role BuiltIn(BuiltInRole builtIn) => roles.Single(role => role.BuiltIn == builtIn);
}
