namespace ControlPlane.Domain.Roles;

/// <summary>
/// The roles of the active tenant, read without tracking, so a command finds the role of a membership or an invitation, or the built-in
/// role it assigns, and a rule asks what that role may do (A1). A command never changes a role.
/// </summary>
internal sealed class TenantRoles(IReadOnlyList<Role> roles)
{
    /// <summary>The role with this id. Every membership and invitation of the tenant has one of the tenant's roles.</summary>
    public Role Of(Guid roleId) => roles.Single(role => role.Id == roleId);

    /// <summary>The tenant's built-in role of this kind, for an assignment.</summary>
    public Role BuiltIn(BuiltInRole builtIn) => roles.Single(role => role.BuiltIn == builtIn);
}
