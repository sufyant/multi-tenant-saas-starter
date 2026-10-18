using ControlPlane.Contracts.Users;
using SharedKernel;

namespace ControlPlane.Application.Users.RenameUser;

// The command sets a state (I1): the names the user has answer as they are, and nothing is announced. It locks the user row, so two
// changes of the user run one at a time (K6l). Rejections come first (W7).
public static class RenameUserHandler
{
    public static async Task<(Result<UserView>, UserRenamedEvent?)> HandleAsync(
        RenameUserCommand command,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, null);
        }

        var isSystemAdmin = await users.IsSystemAdminAsync(user.Id, cancellationToken);
        var renamed = user.Rename(command.FirstName, command.LastName);
        if (!renamed.IsSuccess || renamed.Value == StateChange.Unchanged)
        {
            return (renamed.Map(_ => UserView.Of(user, isSystemAdmin)), null);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        await users.SaveChangesAsync(cancellationToken);
        var now = time.GetUtcNow();
        return (UserView.Of(user, isSystemAdmin), new UserRenamedEvent(Guid.CreateVersion7(now), now, user.Id, actor));
    }
}
