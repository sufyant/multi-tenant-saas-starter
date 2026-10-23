namespace ControlPlane.Api.Tenant.Roles;

/// <summary>The name of the custom role, and the names of its permissions, from the tenant pool.</summary>
public sealed record CreateRoleRequest(string Name, IReadOnlyList<string> Permissions);
