using ControlPlane.Application.Users;
using ControlPlane.Domain;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.SystemAdmins;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Users;
using ControlPlane.Domain.VerificationCodes;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence.Users;

/// <summary>
/// The users in control_plane.users, which belongs to no tenant (R6), for the commands. A user command's message has no tenant,
/// so the transaction declares none; it declares the user only to read the user's own memberships (R11).
/// </summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class UserRepository(ControlPlaneDbContext controlPlane) : IUserRepository
{
    async Task<Guid?> IUserRepository.FindUserIdAsync(string externalUserId, CancellationToken cancellationToken) =>
        await controlPlane.Users.Where(user => user.ExternalId == externalUserId).Select(user => (Guid?)user.Id).SingleOrDefaultAsync(cancellationToken);

    Task<UserProfile?> IUserRepository.FindProfileAsync(string externalUserId, CancellationToken cancellationToken) =>
        controlPlane.Users
            .Where(user => user.ExternalId == externalUserId)
            .Select(user => new UserProfile(user.Id, user.Email, user.Name, user.Language, user.DeletedAt != null))
            .SingleOrDefaultAsync(cancellationToken);

    Task<UserProfile?> IUserRepository.FindProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.Users
            .Where(user => user.Id == userId)
            .Select(user => new UserProfile(user.Id, user.Email, user.Name, user.Language, user.DeletedAt != null))
            .SingleOrDefaultAsync(cancellationToken);

    Task<UserIdentity?> IUserRepository.FindIdentityAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.Users
            .Where(user => user.Id == userId)
            .Select(user => new UserIdentity(user.ExternalId, user.Email, user.DeletedAt != null))
            .SingleOrDefaultAsync(cancellationToken);

    async Task<UserStatus?> IUserRepository.FindStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.Users.Where(user => user.Id == userId).Select(User.StatusOf).ToListAsync(cancellationToken);
        return found is [var status] ? status : null;
    }

    Task<bool> IUserRepository.UserExistsAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.Users.AnyAsync(user => user.Id == userId, cancellationToken);

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
    // references the user be inserted, which takes FOR KEY SHARE. A person who is not a user yet has no row to lock, so a transaction
    // advisory lock on the identity provider id comes first: the key's first half names the users table, so no other lock shares the
    // key, and the second is the id's hash. A second transaction waits for the first to end, and its next statement then sees the user
    // the first one added. Two ids with the same hash only wait for each other.
    async Task<User?> IUserRepository.FindUserForNoKeyUpdateAsync(string externalUserId, CancellationToken cancellationToken)
    {
        await controlPlane.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock('control_plane.users'::regclass::oid::int, hashtext({externalUserId}))", cancellationToken);
        var found = await controlPlane.Users
            .FromSql($"SELECT * FROM control_plane.users WHERE external_id = {externalUserId} FOR NO KEY UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    Task<bool> IUserRepository.IsSystemAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.AnyAsync(admin => admin.UserId == userId, cancellationToken);

    Task<DateTimeOffset?> IUserRepository.FindSystemAdminGrantedAtAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.SystemAdmins.Where(admin => admin.UserId == userId).Select(admin => (DateTimeOffset?)admin.GrantedAt).SingleOrDefaultAsync(cancellationToken);

    async Task<SystemAdmin?> IUserRepository.FindSystemAdminForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var found = await controlPlane.SystemAdmins
            .FromSql($"SELECT * FROM control_plane.system_admins WHERE user_id = {userId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return found.SingleOrDefault();
    }

    void IUserRepository.AddSystemAdmin(SystemAdmin systemAdmin) => controlPlane.SystemAdmins.Add(systemAdmin);

    void IUserRepository.RemoveSystemAdmin(SystemAdmin systemAdmin) => controlPlane.SystemAdmins.Remove(systemAdmin);

    async Task<IReadOnlyList<UserMembership>> IUserRepository.ListMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        // An Owner is a member whose role holds the Owners' permission (A1, K1).
        var ownerRoles = controlPlane.Roles.Where(Role.Holding(PermissionPools.TenantOwnersManage));
        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        return await (
                from membership in controlPlane.Memberships
                join tenant in controlPlane.Tenants on membership.TenantId equals tenant.Id
                orderby tenant.Id
                select new UserMembership(tenant.Id, ownerRoles.Any(role => role.Id == membership.RoleId)))
            .ToListAsync(cancellationToken);
    }

    async Task<IReadOnlyList<Guid>> IUserRepository.ListJoinedTenantsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        return await controlPlane.Invitations
            .Where(invitation => invitation.AcceptedBy == userId)
            .Select(invitation => invitation.TenantId)
            .Distinct()
            .Order()
            .ToListAsync(cancellationToken);
    }

    // The lock of the address, for the rest of the transaction, orders two changes to the same address by two users: the second waits for
    // the first to commit, then finds the address taken (Kleppmann, Chapter 7, "Write Skew and Phantoms"). A user's own lock orders only
    // that user's changes.
    async Task<bool> IUserRepository.IsEmailTakenAsync(EmailAddress email, Guid exceptUserId, CancellationToken cancellationToken)
    {
        await controlPlane.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"email:" + email.Value}, 0))", cancellationToken);
        return await controlPlane.Users.AnyAsync(user => user.Email == email.Value && user.Id != exceptUserId, cancellationToken);
    }

    async Task<VerificationCode?> IUserRepository.FindLatestVerificationCodeForUpdateAsync(
        Guid userId, VerificationCodePurpose purpose, CancellationToken cancellationToken)
    {
        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        var found = await controlPlane.VerificationCodes
            .FromSql($"SELECT * FROM control_plane.verification_codes WHERE user_id = {userId} AND purpose = {purpose.ToString()} ORDER BY created_at DESC, id DESC LIMIT 1 FOR UPDATE")
            .ToListAsync(cancellationToken);
        return found.SingleOrDefault();
    }

    async Task<int> IUserRepository.CountVerificationCodesSinceAsync(
        Guid userId, VerificationCodePurpose purpose, DateTimeOffset since, CancellationToken cancellationToken)
    {
        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        return await controlPlane.VerificationCodes.CountAsync(code => code.UserId == userId && code.Purpose == purpose && code.CreatedAt >= since, cancellationToken);
    }

    async Task IUserRepository.DeleteVerificationCodesBeforeAsync(Guid userId, DateTimeOffset before, CancellationToken cancellationToken)
    {
        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        await controlPlane.VerificationCodes.Where(code => code.UserId == userId && code.CreatedAt < before).ExecuteDeleteAsync(cancellationToken);
    }

    void IUserRepository.AddVerificationCode(VerificationCode code) => controlPlane.VerificationCodes.Add(code);

    async Task IUserRepository.DeleteOwnRecordsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await controlPlane.DeclareUserAsync(userId, cancellationToken);
        await controlPlane.VerificationCodes.Where(code => code.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await controlPlane.StoredFiles.Where(file => file.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }

    void IUserRepository.Add(User user) => controlPlane.Users.Add(user);

    Task IUserRepository.SaveChangesAsync(CancellationToken cancellationToken) => controlPlane.SaveChangesAsync(cancellationToken);
}
