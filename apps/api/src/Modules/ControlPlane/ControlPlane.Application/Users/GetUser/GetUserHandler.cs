using ControlPlane.Application.Ports;
using SharedKernel;

namespace ControlPlane.Application.Users.GetUser;

public static class GetUserHandler
{
    public static async Task<Result<UserDetailView>> HandleAsync(GetUserQuery query, IUserCatalog users, CancellationToken cancellationToken)
    {
        if (await users.FindUserAsync(query.UserId, cancellationToken) is not { } user)
        {
            return Errors.UserNotFound;
        }

        var suspendedBy = user.SuspendedBy is { } suspenderId ? await users.FindUserAsync(suspenderId, cancellationToken) : null;
        var isSystemAdmin = await users.IsSystemAdminAsync(user.Id, cancellationToken);
        return new UserDetailView(
            UserView.Of(user, isSystemAdmin),
            user.SuspendedAt,
            suspendedBy is null ? null : UserNameView.Of(suspendedBy),
            await users.ListMembershipDetailsAsync(user.Id, cancellationToken));
    }
}
