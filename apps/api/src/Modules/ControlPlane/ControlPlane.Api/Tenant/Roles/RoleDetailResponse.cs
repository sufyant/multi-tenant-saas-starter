namespace ControlPlane.Api.Tenant.Roles;

/// <summary>
/// A role of the tenant as the role endpoints answer it: what <see cref="RoleResponse"/> holds, and the names of the role's
/// permissions, in ordinal order.
/// </summary>
public sealed record RoleDetailResponse(Guid Id, string Name, bool BuiltIn, IReadOnlyList<string> Permissions);
