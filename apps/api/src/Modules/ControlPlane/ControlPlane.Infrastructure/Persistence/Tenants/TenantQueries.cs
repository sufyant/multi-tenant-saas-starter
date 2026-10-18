using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.ListMyTenants;
using ControlPlane.Application.Tenants.ListTenants;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using ControlPlane.Infrastructure.Persistence.Lists;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

/// <summary>
/// The tenants for the queries, read without tracking. One tenant is read inside the active tenant (W2); the lists run outside any
/// tenant: the platform's list sees every tenant, and a user's list declares the user (R11), so the own_memberships policy decides what
/// is seen. The message of a user's list has no tenant, so the transaction declares none (R4).
/// </summary>
/// <remarks>Public only so that Wolverine's generated code can build it on the handler's own DbContext (W1, W9).</remarks>
public sealed class TenantQueries(ControlPlaneDbContext controlPlane) : ITenantQueries
{
    Task<Tenant?> ITenantQueries.FindTenantAsync(CancellationToken cancellationToken) =>
        controlPlane.Tenants.AsNoTracking().SingleOrDefaultAsync(tenant => tenant.Id == controlPlane.ActiveTenant, cancellationToken);

    // The tenants table belongs to no tenant, so the list needs no active tenant and sees every tenant. Its summary names the first Owner,
    // and the users table is not under row level security either (R6): one query joins each tenant's first Owner, so the page is one round
    // trip, with no query for each tenant.
    async Task<ListPage<(Tenant Tenant, User? FirstOwner)>> ITenantQueries.ListTenantsAsync(ListRequest<ListTenantsSort> list, TenantStatus? status, CancellationToken cancellationToken)
    {
        var tenants = controlPlane.Tenants.AsQueryable();
        if (status is { } wanted)
        {
            tenants = tenants.Where(tenant => tenant.Status == wanted);
        }

        if (list.Search is { } search)
        {
            var pattern = ListQueryExtensions.ContainsPattern(search);
            tenants = tenants.Where(tenant =>
                EF.Functions.ILike(tenant.Name, pattern, ListQueryExtensions.Escape) || EF.Functions.ILike(tenant.Slug, pattern, ListQueryExtensions.Escape));
        }

        var total = await tenants.CountAsync(cancellationToken);
        var rows =
            from tenant in tenants
            join user in controlPlane.Users on tenant.FirstOwnerId equals user.Id into owners
            from owner in owners.DefaultIfEmpty()
            select new { Tenant = tenant, Owner = owner };
        var sorted = list.Sort switch
        {
            ListTenantsSort.Slug => rows.OrderBy(row => row.Tenant.Slug, list.Descending),
            _ => rows.OrderBy(row => row.Tenant.Name, list.Descending),
        };
        var page = await sorted
            .ThenBy(row => row.Tenant.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ListPage.Of([.. page.Select(row => (row.Tenant, (User?)row.Owner))], list.Paging, total);
    }

    // Row level security keeps the memberships to the active tenant; the users table is not under it (R6), so the query starts from them.
    async Task<IReadOnlyList<User>> ITenantQueries.ListOwnersAsync(CancellationToken cancellationToken) =>
        await (
                from membership in controlPlane.TenantMemberships
                where membership.RoleId == BuiltInRoles.Owner.Id
                join user in controlPlane.Users on membership.UserId equals user.Id
                orderby membership.JoinedAt, membership.UserId
                select user)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    // The catalog user is found by the identity provider's id in control_plane.users, which belongs to no tenant. The page is one query:
    // each membership's tenant and role are joined, with no query for each tenant.
    async Task<ListPage<(Tenant Tenant, Role Role)>> ITenantQueries.ListPastCreationAsync(string externalUserId, ListRequest<ListMyTenantsSort> list, CancellationToken cancellationToken)
    {
        var userId = await controlPlane.Users
            .Where(user => user.ExternalId == externalUserId)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is not { } id)
        {
            return ListPage.Of<(Tenant, Role)>([], list.Paging, 0);
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
            var pattern = ListQueryExtensions.ContainsPattern(search);
            tenants = tenants.Where(row =>
                EF.Functions.ILike(row.Tenant.Name, pattern, ListQueryExtensions.Escape) || EF.Functions.ILike(row.Tenant.Slug, pattern, ListQueryExtensions.Escape));
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

        return ListPage.Of([.. page.Select(row => (row.Tenant, row.Role))], list.Paging, total);
    }
}
