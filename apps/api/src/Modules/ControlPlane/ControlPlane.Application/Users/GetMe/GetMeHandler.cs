using SharedKernel;

namespace ControlPlane.Application.Users.GetMe;

public static class GetMeHandler
{
    public static async Task<Result<MeView>> HandleAsync(GetMeQuery query, IUserQueries users, CancellationToken cancellationToken)
    {
        if (await users.FindUserAsync(query.ActorId, cancellationToken) is not { } user)
        {
            return Errors.UserNotFound;
        }

        return new MeView(
            user.Id, user.Email, user.FirstName, user.LastName, await users.IsSystemAdminAsync(user.Id, cancellationToken), user.Language, user.TimeZone);
    }
}
