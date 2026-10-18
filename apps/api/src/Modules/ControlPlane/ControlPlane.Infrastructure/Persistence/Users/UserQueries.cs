using ControlPlane.Application.Members;
using ControlPlane.Application.Users;
using ControlPlane.Application.Users.ListUsers;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Users;
using ControlPlane.Infrastructure.Persistence.Lists;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence.Users;

/// <summary>
/// The catalog users in control_plane.users, which belongs to no tenant (R6), for the queries, read without tracking. The message has no
/// tenant, so the transaction declares none; it declares the user only to read the user's own memberships (R11).
/// </summary>
/// <remarks>Public only so that Wolverine's generated code can build it on the handler's own DbContext (W1, W9).</remarks>
public sealed class UserQueries(ControlPlaneDbContext controlPlane) : IUserQueries
{
    Task<User?> IUserQueries.FindUserAsync(string externalUserId, CancellationToken cancellationToken) =>
        controlPlane.Users.AsNoTracking().SingleOrDefaultAsync(user => user.ExternalId == externalUserId, cancellationToken);

    Task<User?> IUserQueries.FindUserAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    Task<bool> IUserQueries.IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => admin.UserId == userId, cancellationToken);

    // The tenants table is not under row level security (R6), so the one query joins each membership's tenant and role. The tenant's
    // summary counts its Owners (R12): a user who holds the Owner role in a tenant with one Owner is its last Owner.
    async Task<IReadOnlyList<UserMembershipView>> IUserQueries.ListMembershipDetailsAsync(Guid userId, CancellationToken cancellationToken)
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
            .. memberships.Select(membership => new UserMembershipView(
                membership.Id,
                membership.Name,
                membership.Slug,
                membership.Status.ToString(),
                RoleView.Of(membership.BuiltIn),
                membership.JoinedAt,
                membership.BuiltIn == BuiltInRole.Owner && membership.OwnerCount == 1)),
        ];
    }

    async Task<ListPage<(User User, bool IsSystemAdmin)>> IUserQueries.ListUsersAsync(
        ListRequest<ListUsersSort> list,
        UserStatus? status,
        CancellationToken cancellationToken)
    {
        var users = status is { } chosen ? controlPlane.Users.Where(User.InStatus(chosen)) : controlPlane.Users;

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
}
