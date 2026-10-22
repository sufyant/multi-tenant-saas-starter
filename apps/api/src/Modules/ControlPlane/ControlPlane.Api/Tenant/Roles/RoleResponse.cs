namespace ControlPlane.Api.Tenant.Roles;

/// <summary>
/// A role as a member, an invitation or a tenant of the signed-in user holds it: its id, its name and whether it is built in. A built-in
/// role answers its stored name, <c>owner</c>, <c>admin</c> or <c>member</c>; a custom role answers the name the tenant gave it (A2, A3).
/// A request gives a role by its id.
/// </summary>
public sealed record RoleResponse(Guid Id, string Name, bool BuiltIn);
