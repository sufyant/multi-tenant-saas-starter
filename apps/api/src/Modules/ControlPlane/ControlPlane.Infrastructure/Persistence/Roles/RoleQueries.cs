using ControlPlane.Application.Roles;
using ControlPlane.Application.Roles.ListRoles;
using ControlPlane.Infrastructure.Persistence.Lists;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence.Roles;

/// <summary>
/// The active tenant's roles for the queries, read without tracking. Row level security keeps the roles and their permissions to the
/// tenant; the queries add no filter of their own.
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class RoleQueries(ControlPlaneDbContext controlPlane) : IRoleQueries
{
    // Row level security keeps the list to the active tenant; the query names no tenant. The page reads each role's permissions with it,
    // so no query runs for each role. Two roles never share a name in a tenant, and the id breaks every tie (K5).
    async Task<ListPage<RoleDetailView>> IRoleQueries.ListRolesAsync(ListRequest<ListRolesSort> list, CancellationToken cancellationToken)
    {
        var roles = controlPlane.TenantRoles;
        if (list.Search is { } search)
        {
            var pattern = ListQueryExtensions.ContainsPattern(search);
            roles = roles.Where(role => EF.Functions.ILike(role.Name, pattern, ListQueryExtensions.Escape));
        }

        var total = await roles.CountAsync(cancellationToken);
        var page = await roles
            .OrderBy(role => role.Name, list.Descending)
            .ThenBy(role => role.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .Include(role => role.Grants)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ListPage.Of([.. page.Select(RoleDetailView.Of)], list.Paging, total);
    }
}
