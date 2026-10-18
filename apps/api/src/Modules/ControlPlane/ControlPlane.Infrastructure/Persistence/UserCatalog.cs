using ControlPlane.Application.Members;
using ControlPlane.Application.Ports;
using ControlPlane.Application.Users;
using ControlPlane.Application.Users.UserDeletion;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence;

/// <summary>
/// The catalog users in control_plane.users, which belongs to no tenant (R6). The message has no tenant, so the transaction declares none;
/// it declares the user only to read the user's own memberships (R11).
/// </summary>
/// <remarks>Public only so that Wolverine's generated code can build it on the handler's own DbContext (W1, W9).</remarks>
public sealed class UserCatalog(ControlPlaneDbContext controlPlane) : IUserCatalog
{
    async Task<User?> IUserCatalog.FindUserForUpdateAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Users
            .FromSql($"SELECT * FROM control_plane.users WHERE external_id = {externalUserId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    async Task<User?> IUserCatalog.FindUserForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Users
            .FromSql($"SELECT * FROM control_plane.users WHERE id = {userId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    Task<User?> IUserCatalog.FindUserAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    async Task<IReadOnlyList<UserMembership>> IUserCatalog.ListMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        return await (
                from membership in controlPlane.Memberships
                join tenant in controlPlane.Tenants on membership.TenantId equals tenant.Id
                orderby tenant.Id
                select new UserMembership(
                    tenant.Id,
                    membership.RoleId == BuiltInRoles.Owner.Id
                    && (tenant.Status == TenantStatus.Active || tenant.Status == TenantStatus.Suspended || tenant.Status == TenantStatus.Closed)))
            .ToListAsync(cancellationToken);
    }

    // The tenants table is not under row level security (R6), so the one query joins each membership's tenant and role. The tenant's
    // summary counts its Owners (R12): a user who holds the Owner role in a tenant with one Owner is its only Owner.
    async Task<IReadOnlyList<UserMembershipDetails>> IUserCatalog.ListMembershipDetailsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        var memberships = await (
                from membership in controlPlane.Memberships
                join tenant in controlPlane.Tenants on membership.TenantId equals tenant.Id
                join role in controlPlane.Roles on membership.RoleId equals role.Id
                orderby tenant.Name, tenant.Id
                select new { tenant.Id, tenant.Name, tenant.Slug, tenant.Status, role.BuiltIn, membership.JoinedAt, tenant.OwnerCount })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return
        [
            .. memberships.Select(membership => new UserMembershipDetails(
                membership.Id,
                membership.Name,
                membership.Slug,
                membership.Status.ToString(),
                RoleSummary.Of(membership.BuiltIn),
                membership.JoinedAt,
                membership.BuiltIn == BuiltInRole.Owner && membership.OwnerCount == 1)),
        ];
    }

    Task<User?> IUserCatalog.FindUserAsync(string externalUserId, CancellationToken cancellationToken) =>
        controlPlane.Users.AsNoTracking().SingleOrDefaultAsync(user => user.ExternalId == externalUserId, cancellationToken);

    async Task<Guid?> IUserCatalog.FindUserIdAsync(string externalUserId, CancellationToken cancellationToken) =>
        await controlPlane.Users.Where(user => user.ExternalId == externalUserId).Select(user => (Guid?)user.Id).SingleOrDefaultAsync(cancellationToken);

    Task<bool> IUserCatalog.IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => admin.UserId == userId, cancellationToken);

    // The status is derived as UserDetails derives it: a deletion outranks a suspension.
    async Task<ListPage<(User User, bool IsSystemAdmin)>> IUserCatalog.ListUsersAsync(
        ListRequest<ListUsersSort> list,
        string? status,
        CancellationToken cancellationToken)
    {
        var users = status switch
        {
            "Deleted" => controlPlane.Users.Where(user => user.DeletedAt != null),
            "Suspended" => controlPlane.Users.Where(user => user.DeletedAt == null && user.SuspendedAt != null),
            "Active" => controlPlane.Users.Where(user => user.DeletedAt == null && user.SuspendedAt == null),
            null => controlPlane.Users,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "A user is active, suspended or deleted."),
        };

        if (list.Search is { } search)
        {
            var pattern = ListQueries.ContainsPattern(search);
            users = users.Where(user =>
                EF.Functions.ILike(user.Email!, pattern, ListQueries.Escape)
                || EF.Functions.ILike(user.FirstName!, pattern, ListQueries.Escape)
                || EF.Functions.ILike(user.LastName!, pattern, ListQueries.Escape));
        }

        var total = await users.CountAsync(cancellationToken);
        var sorted = list.Sort switch
        {
            ListUsersSort.LastName => users.OrderBy(user => user.LastName == null).ThenBy(user => user.LastName, list.Descending),
            _ => users.OrderBy(user => user.Email == null).ThenBy(user => user.Email, list.Descending),
        };
        var page = await sorted
            .ThenBy(user => user.Id)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .Select(user => new { User = user, IsSystemAdmin = controlPlane.SystemAdmins.Any(admin => admin.UserId == user.Id) })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ListPage.Of([.. page.Select(found => (found.User, found.IsSystemAdmin))], list.Paging, total);
    }

    Task IUserCatalog.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
