using ControlPlane.Application.Users;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Users;

/// <summary>
/// The catalog users in control_plane.users, which belongs to no tenant (R6), for the commands. A user command's message has no tenant,
/// so the transaction declares none; it declares the user only to read the user's own memberships (R11).
/// </summary>
/// <remarks>Public only so that Wolverine's generated code can build it on the handler's own DbContext (W1, W9).</remarks>
public sealed class UserRepository(ControlPlaneDbContext controlPlane) : IUserRepository
{
    Task<User?> IUserRepository.FindUserAsync(string externalUserId, CancellationToken cancellationToken) =>
        controlPlane.Users.SingleOrDefaultAsync(user => user.ExternalId == externalUserId, cancellationToken);

    Task<User?> IUserRepository.FindUserAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    async Task<Guid?> IUserRepository.FindUserIdAsync(string externalUserId, CancellationToken cancellationToken) =>
        await controlPlane.Users.Where(user => user.ExternalId == externalUserId).Select(user => (Guid?)user.Id).SingleOrDefaultAsync(cancellationToken);

    async Task<User?> IUserRepository.FindUserForUpdateAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Users
            .FromSql($"SELECT * FROM control_plane.users WHERE external_id = {externalUserId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    async Task<User?> IUserRepository.FindUserForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Users
            .FromSql($"SELECT * FROM control_plane.users WHERE id = {userId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    // FOR NO KEY UPDATE: the lock an update of the row takes. It waits for a deletion's FOR UPDATE, and it lets a membership that
    // references the user be inserted, which takes FOR KEY SHARE.
    async Task<User?> IUserRepository.FindUserForNoKeyUpdateAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Users
            .FromSql($"SELECT * FROM control_plane.users WHERE external_id = {externalUserId} FOR NO KEY UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    Task<bool> IUserRepository.IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => admin.UserId == userId, cancellationToken);

    async Task<IReadOnlyList<UserMembership>> IUserRepository.ListMembershipsAsync(Guid userId, CancellationToken cancellationToken)
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

    void IUserRepository.Add(User user) => controlPlane.Users.Add(user);

    Task IUserRepository.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
