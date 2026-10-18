using SharedKernel;

namespace ControlPlane.Application.Users.ListUserMemberships;

// A system admin is never deleted, as they are never suspended: the system door stays open to system admins, and the first system admin is
// written only while the system admin list is empty.
public static class ListUserMembershipsHandler
{
    public static async Task<Result<UserMembership[]>> HandleAsync(
        ListUserMembershipsQuery query,
        IUserRepository users,
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
