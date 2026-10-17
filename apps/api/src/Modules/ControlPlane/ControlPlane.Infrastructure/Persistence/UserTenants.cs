using ControlPlane.Application.Ports;
using ControlPlane.Application.Tenants;
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
    async Task<ListPage<Tenant>> IUserTenants.ListPastCreationAsync(string externalUserId, ListRequest<ListMyTenantsSort> list, CancellationToken cancellationToken)
    {
        var userId = await controlPlane.Users
            .Where(user => user.ExternalId == externalUserId)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is not { } id)
        {
            return new ListPage<Tenant>([], list.Paging, 0);
        }

        await controlPlane.DeclareUserAsync(id, cancellationToken);
        var tenants =
            from membership in controlPlane.Memberships
            join tenant in controlPlane.Tenants on membership.TenantId equals tenant.Id
            where tenant.Status == TenantStatus.Active || tenant.Status == TenantStatus.Suspended || tenant.Status == TenantStatus.Closed
            select tenant;
        if (list.Search is { } search)
        {
            var pattern = ListQueries.ContainsPattern(search);
            tenants = tenants.Where(tenant =>
                EF.Functions.ILike(tenant.Name, pattern, ListQueries.Escape) || EF.Functions.ILike(tenant.Slug, pattern, ListQueries.Escape));
        }

        // The list has one sort field, the name.
        var total = await tenants.CountAsync(cancellationToken);
        var page = await tenants
            .OrderBy(tenant => tenant.Name, list.Descending)
            .ThenBy(tenant => tenant.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new ListPage<Tenant>(page, list.Paging, total);
    }
}
