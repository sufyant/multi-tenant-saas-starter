namespace ControlPlane.Application.Users.GetPendingEmailChange;

public static class GetPendingEmailChangeHandler
{
    public static Task<string?> HandleAsync(GetPendingEmailChangeQuery query, IUserQueries users, CancellationToken cancellationToken) =>
        users.FindPendingEmailChangeAsync(query.ActorId, cancellationToken);
}
