using ControlPlane.Application.Ports;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence;

/// <summary>The catalog users in control_plane.users, which belongs to no tenant (R6). The message has no tenant, so the transaction declares none.</summary>
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

    async Task<Guid?> IUserCatalog.FindUserIdAsync(string externalUserId, CancellationToken cancellationToken) =>
        await controlPlane.Users.Where(user => user.ExternalId == externalUserId).Select(user => (Guid?)user.Id).SingleOrDefaultAsync(cancellationToken);

    Task<bool> IUserCatalog.IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => admin.UserId == userId, cancellationToken);

    async Task<ListPage<(User User, bool IsSystemAdmin)>> IUserCatalog.ListUsersAsync(PageRequest paging, CancellationToken cancellationToken)
    {
        var total = await controlPlane.Users.CountAsync(cancellationToken);
        var page = await controlPlane.Users
            .OrderBy(user => user.Email == null)
            .ThenBy(user => user.Email)
            .ThenBy(user => user.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(user => new { User = user, IsSystemAdmin = controlPlane.SystemAdmins.Any(admin => admin.UserId == user.Id) })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new([.. page.Select(found => (found.User, found.IsSystemAdmin))], paging, total);
    }

    Task IUserCatalog.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
