namespace ControlPlane.Api.Tenant.Roles;

/// <summary>The name the custom role takes, and exactly the permissions it holds afterwards, from the tenant pool.</summary>
public sealed record UpdateRoleRequest(string Name, IReadOnlyList<string> Permissions);
