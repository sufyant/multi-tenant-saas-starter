using SharedKernel;

namespace ControlPlane.Application.Roles.ListRoles;

public static class ListRolesHandler
{
    public static async Task<Result<ListPage<RoleDetailView>>> HandleAsync(ListRolesQuery query, IRoleQueries roles, CancellationToken cancellationToken) =>
        await roles.ListRolesAsync(query.List, cancellationToken);
}
