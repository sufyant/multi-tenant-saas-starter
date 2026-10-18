using SharedKernel;

namespace ControlPlane.Application.Users.GetMe;

public static class GetMeHandler
{
    public static async Task<Result<MeView>> HandleAsync(GetMeQuery query, IUserQueries users, CancellationToken cancellationToken) =>
        await users.FindMeAsync(query.ActorId, cancellationToken) is { } me ? me : Errors.UserNotFound;
}
