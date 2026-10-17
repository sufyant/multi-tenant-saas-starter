using ControlPlane.Application.Ports;
using ControlPlane.Application.Tenants;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence;

/// <summary>
/// The catalog user is found by the identity provider's id in control_plane.users, which belongs to no tenant. Their memberships are then
/// read with that user declared in the transaction Wolverine began for the handler (R11), so the own_memberships policy decides
/// what is seen; the query adds no filter of its own. The message has no tenant, so the transaction declares none (R4).
/// </summary>
/// <remarks>Public only so that Wolverine's generated code can build it on the handler's own DbContext (W1, W9).</remarks>
public sealed class UserTenants(ControlPlaneDbContext controlPlane) : IUserTenants
{
    // The page is one query: each membership's tenant and role are joined, with no query for each tenant.
    async Task<ListPage<(Tenant Tenant, Role Role)>> IUserTenants.ListPastCreationAsync(string externalUserId, ListRequest<ListMyTenantsSort> list, CancellationToken cancellationToken)
    {
        var userId = await controlPlane.Users
            .Where(user => user.ExternalId == externalUserId)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is not { } id)
        {
            return new([], list.Paging, 0);
        }

        await controlPlane.DeclareUserAsync(id, cancellationToken);
        var tenants =
            from membership in controlPlane.Memberships
            join tenant in controlPlane.Tenants on membership.TenantId equals tenant.Id
            join role in controlPlane.Roles on membership.RoleId equals role.Id
            where tenant.Status == TenantStatus.Active || tenant.Status == TenantStatus.Suspended || tenant.Status == TenantStatus.Closed
            select new { Tenant = tenant, Role = role };
        if (list.Search is { } search)
        {
            var pattern = ListQueries.ContainsPattern(search);
            tenants = tenants.Where(row =>
                EF.Functions.ILike(row.Tenant.Name, pattern, ListQueries.Escape) || EF.Functions.ILike(row.Tenant.Slug, pattern, ListQueries.Escape));
        }

        // The list has one sort field, the name.
        var total = await tenants.CountAsync(cancellationToken);
        var page = await tenants
            .OrderBy(row => row.Tenant.Name, list.Descending)
            .ThenBy(row => row.Tenant.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new([.. page.Select(row => (row.Tenant, row.Role))], list.Paging, total);
    }
}
