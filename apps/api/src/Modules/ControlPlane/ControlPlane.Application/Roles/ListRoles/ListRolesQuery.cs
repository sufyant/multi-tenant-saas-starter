using SharedKernel;

namespace ControlPlane.Application.Roles.ListRoles;

/// <summary>
/// The roles of the active tenant, built in and custom, a page at a time, sorted by one field, then by id. The search matches the name.
/// </summary>
[MessageName("list-roles")]
public sealed record ListRolesQuery(ListRequest<ListRolesSort> List);
