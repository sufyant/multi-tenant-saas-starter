using ControlPlane.Application.Ports;
using ControlPlane.Domain;
using SharedKernel;

namespace ControlPlane.Application.Users.UserDeletion;

/// <summary>
/// The first step of a user's deletion: the tenants the user is a member of, and whether they are an Owner there. It runs without a
/// tenant, with the user declared (R11), so it sees the user's own memberships in every tenant.
/// </summary>
public sealed record ListUserMemberships(Guid UserId);

/// <summary>A tenant the user is a member of. <see cref="OwnsATenantPastCreation"/>: the user is its Owner, and it is active, suspended or closed.</summary>
public sealed record UserMembership(Guid TenantId, bool OwnsATenantPastCreation);

// A system admin is never deleted, as they are never suspended: the system door stays open to system admins, and the first system admin is
// written only while the system admin list is empty.
public static class ListUserMembershipsHandler
{
    public static async Task<Result<UserMembership[]>> HandleAsync(
        ListUserMemberships query,
        IUserCatalog users,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserAsync(query.UserId, cancellationToken) is null)
        {
            return Errors.UserNotFound;
        }

        if (await users.IsSystemAdminAsync(query.UserId, cancellationToken))
        {
            return Errors.UserIsSystemAdmin;
        }

        return (await users.ListMembershipsAsync(query.UserId, cancellationToken)).ToArray();
    }
}
