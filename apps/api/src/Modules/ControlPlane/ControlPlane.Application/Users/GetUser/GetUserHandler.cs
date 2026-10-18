using SharedKernel;

namespace ControlPlane.Application.Users.GetUser;

public static class GetUserHandler
{
    public static async Task<Result<UserDetailView>> HandleAsync(GetUserQuery query, IUserQueries users, CancellationToken cancellationToken) =>
        await users.FindUserDetailAsync(query.UserId, cancellationToken) is { } user ? user : Errors.UserNotFound;
}
