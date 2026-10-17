using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Users;

/// <summary>
/// One catalog user, for a system admin: the user detail screen. It runs without a tenant, and declares the user to read their
/// memberships in every tenant (R11), as a user's deletion does.
/// </summary>
public sealed record GetUser(Guid UserId);

/// <summary>
/// A user as in the list, with when a system admin suspended them and that system admin's names, and every membership by the tenant's
/// name, then id.
/// </summary>
public sealed record UserWithMemberships(
    UserDetails User,
    DateTimeOffset? SuspendedAt,
    PersonName? SuspendedBy,
    IReadOnlyList<UserMembershipDetails> Memberships);

/// <summary>
/// A tenant the user is a member of: its name, slug and status (<c>Active</c>, <c>Suspended</c>, <c>Closed</c>), the name of the
/// built-in role the user holds there (<c>owner</c>), when they joined, and whether they are its only Owner. The suspend dialog warns
/// with it, and the delete refusal names those tenants.
/// </summary>
public sealed record UserMembershipDetails(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string TenantStatus,
    string Role,
    DateTimeOffset JoinedAt,
    bool SoleOwner);

// A version before step 20l reactivates a user by clearing the moment only, so who suspended a user is read only while they are suspended.
public static class GetUserHandler
{
    public static async Task<Result<UserWithMemberships>> HandleAsync(GetUser query, IUserCatalog users, CancellationToken cancellationToken)
    {
        if (await users.FindUserAsync(query.UserId, cancellationToken) is not { } user)
        {
            return Errors.UserNotFound;
        }

        var suspendedBy = (user.SuspendedAt, user.SuspendedBy) is ({ }, { } adminId) ? await users.FindUserAsync(adminId, cancellationToken) : null;
        var isSystemAdmin = await users.IsSystemAdminAsync(user.Id, cancellationToken);
        return new UserWithMemberships(
            UserDetails.Of(user, isSystemAdmin),
            user.SuspendedAt,
            suspendedBy is null ? null : PersonName.Of(suspendedBy),
            await users.ListMembershipDetailsAsync(user.Id, cancellationToken));
    }
}
