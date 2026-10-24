using ControlPlane.Application.Invitations.GetInvitationPreview;
using ControlPlane.Application.Members;
using ControlPlane.Application.SystemAdmins;
using ControlPlane.Application.Users;
using ControlPlane.Application.Users.GetMe;
using ControlPlane.Application.Users.GetUser;
using ControlPlane.Application.Users.ListUsers;
using ControlPlane.Domain;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using ControlPlane.Infrastructure.Persistence.Lists;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence.Users;

/// <summary>
/// The users in control_plane.users, which belongs to no tenant (R6), for the queries, read without tracking. The message has no
/// tenant, so the transaction declares none; it declares the user only to read the user's own memberships (R11).
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class UserQueries(ControlPlaneDbContext controlPlane, SystemAdminOptions systemAdmins) : IUserQueries
{
    async Task<MeView?> IUserQueries.FindMeAsync(string externalUserId, CancellationToken cancellationToken)
    {
        if (await controlPlane.Users.AsNoTracking().SingleOrDefaultAsync(user => user.ExternalId == externalUserId, cancellationToken) is not { } user)
        {
            return null;
        }

        return new MeView(user.Id, user.Email, user.FirstName, user.LastName, await IsSystemAdminAsync(user.Id, cancellationToken), user.Language, user.TimeZone);
    }

    // The user, the system admin who suspended them, when they were granted system admin, and their memberships: the four queries, in the
    // order of the read before the views.
    async Task<UserDetailView?> IUserQueries.FindUserDetailAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await FindAsync(userId, cancellationToken) is not { } user)
        {
            return null;
        }

        var suspendedBy = user.SuspendedBy is { } suspenderId ? await FindAsync(suspenderId, cancellationToken) : null;
        var systemAdminGrantedAt = await controlPlane.SystemAdmins
            .Where(admin => admin.UserId == user.Id)
            .Select(admin => (DateTimeOffset?)admin.GrantedAt)
            .SingleOrDefaultAsync(cancellationToken);
        return new UserDetailView(
            UserView.Of(user, systemAdminGrantedAt, systemAdmins),
            user.SuspendedAt,
            suspendedBy is null ? null : UserNameView.Of(suspendedBy),
            await ListMembershipsAsync(user.Id, cancellationToken));
    }

    // A system admin invites as the platform, so the screen never sees a system admin's name.
    async Task<InviterNameView?> IUserQueries.FindInviterNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await FindAsync(userId, cancellationToken) is not { } user)
        {
            return null;
        }

        return await IsSystemAdminAsync(user.Id, cancellationToken)
            ? new InviterNameView(nameof(InviterKind.Platform), null)
            : new InviterNameView(nameof(InviterKind.User), UserNameView.Of(user));
    }

    async Task<ListPage<UserView>> IUserQueries.ListUsersAsync(
        ListRequest<ListUsersSort> list,
        UserStatus? status,
        bool? isSystemAdmin,
        EmailAddress? email,
        CancellationToken cancellationToken)
    {
        var users = status is { } chosen ? controlPlane.Users.Where(User.InStatus(chosen)) : controlPlane.Users;
        if (isSystemAdmin is { } admins)
        {
            users = users.Where(user => controlPlane.SystemAdmins.Any(admin => admin.UserId == user.Id) == admins);
        }

        // Plain equality on the stored address, which is in its one form too, through the B-tree on users.email (section 6, "Rules
        // without an id").
        if (email is { Value: var address })
        {
            users = users.Where(user => user.Email == address);
        }

        if (list.Search is { } search)
        {
            var pattern = ListQueryExtensions.ContainsPattern(search);
            users = users.Where(user =>
                EF.Functions.ILike(user.Email!, pattern, ListQueryExtensions.Escape)
                || EF.Functions.ILike(user.FirstName!, pattern, ListQueryExtensions.Escape)
                || EF.Functions.ILike(user.LastName!, pattern, ListQueryExtensions.Escape));
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
            .Select(user => new
            {
                User = user,
                GrantedAt = controlPlane.SystemAdmins.Where(admin => admin.UserId == user.Id).Select(admin => (DateTimeOffset?)admin.GrantedAt).SingleOrDefault(),
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ListPage.Of([.. page.Select(found => UserView.Of(found.User, found.GrantedAt, systemAdmins))], list.Paging, total);
    }

    private Task<User?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    private Task<bool> IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => admin.UserId == userId, cancellationToken);

    // The tenants table is not under row level security (R6), so the one query joins each membership's tenant and role. The tenant's
    // summary counts its Owners (R12): a user whose role holds the Owners' permission (A1, K1) in a tenant with one Owner is its last Owner.
    // The query asks it with Role.Holding, which reads no permission rows for an owner-only permission: the transaction declares the user,
    // and row level security shows it no row of role_permissions (R11).
    private async Task<IReadOnlyList<UserMembershipView>> ListMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        var ownerRoles = controlPlane.Roles.Where(Role.Holding(PermissionPools.TenantOwnersManage));
        var memberships = await (
                from membership in controlPlane.Memberships
                join tenant in controlPlane.Tenants on membership.TenantId equals tenant.Id
                join summary in controlPlane.TenantSummaries on tenant.Id equals summary.TenantId
                join role in controlPlane.Roles on membership.RoleId equals role.Id
                orderby tenant.Name, tenant.Id
                select new
                {
                    tenant.Id,
                    tenant.Name,
                    tenant.Slug,
                    tenant.Status,
                    Role = role,
                    membership.JoinedAt,
                    summary.OwnerCount,
                    ManagesOwners = ownerRoles.Any(owner => owner.Id == role.Id),
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return
        [
            .. memberships.Select(membership => new UserMembershipView(
                membership.Id,
                membership.Name,
                membership.Slug,
                membership.Status.ToString(),
                RoleView.Of(membership.Role),
                membership.JoinedAt,
                membership.ManagesOwners && membership.OwnerCount == 1)),
        ];
    }
}
