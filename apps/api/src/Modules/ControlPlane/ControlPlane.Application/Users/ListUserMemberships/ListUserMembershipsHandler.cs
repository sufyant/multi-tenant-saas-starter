using SharedKernel;

namespace ControlPlane.Application.Users.ListUserMemberships;

// A system admin is never deleted, as they are never suspended: the system door stays open to system admins. A system admin who is no root
// is revoked first (section 6, "Root system admins").
public static class ListUserMembershipsHandler
{
    public static async Task<Result<UserMembership[]>> HandleAsync(
        ListUserMembershipsQuery query,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        if (!await users.UserExistsAsync(query.UserId, cancellationToken))
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
