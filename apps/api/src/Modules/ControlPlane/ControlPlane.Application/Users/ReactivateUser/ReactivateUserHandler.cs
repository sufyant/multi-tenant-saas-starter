using ControlPlane.Contracts.Users;
using SharedKernel;

namespace ControlPlane.Application.Users.ReactivateUser;

// The command sets a state (I1): a user who is not suspended answers as they are, and nothing is announced.
public static class ReactivateUserHandler
{
    public static async Task<(Result<UserView>, UserReactivatedEvent?)> HandleAsync(
        ReactivateUserCommand command,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, null);
        }

        var isSystemAdmin = await users.IsSystemAdminAsync(user.Id, cancellationToken);
        if (user.Reactivate() == StateChange.Unchanged)
        {
            return (UserView.Of(user, isSystemAdmin), null);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a catalog user.");
        await users.SaveChangesAsync(cancellationToken);
        var now = time.GetUtcNow();
        return (UserView.Of(user, isSystemAdmin), new UserReactivatedEvent(Guid.CreateVersion7(now), now, user.Id, actor));
    }
}
