using ControlPlane.Application.Roles.ListRoles;
using SharedKernel;

namespace ControlPlane.Application.Roles;

/// <summary>
/// The active tenant's roles for the queries: each method reads without tracking and answers a read model, built in the query (Q2). Row
/// level security keeps the roles and their permissions to the tenant. It never loads a role to change it; <see cref="Tenants.ITenantRepository"/>
/// does.
/// </summary>
/// <remarks>Public because a public handler takes it (W9).</remarks>
public interface IRoleQueries
{
    /// <summary>
    /// A page of the active tenant's roles, built in and custom, with their permissions, whose name contains the search: by the sort
    /// field, then by id.
    /// </summary>
    internal Task<ListPage<RoleDetailView>> ListRolesAsync(ListRequest<ListRolesSort> list, CancellationToken cancellationToken);
}
