using ControlPlane.Application.Users.GetMe;
using ControlPlane.Contracts.Users;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Users.RenameMe;

// The command sets a state (I1): the names the user has answer as they are, and nothing is written or announced. It locks the user row,
// so two changes of the user run one at a time (K6l), and a deletion that commits while it waits is refused (A9). Rejections come first
// (W7). The user acted for themselves, so the event names them as the one who renamed (K3).
public static class RenameMeHandler
{
    public static async Task<(Result<MeView>, OutgoingMessages)> HandleAsync(
        RenameMeCommand command,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // A person becomes a user only through an invitation or as a root system admin at the system door, as GetMe answers.
        if (await users.FindUserForUpdateAsync(command.ActorId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, []);
        }

        var renamed = user.RenameMe(command.FirstName, command.LastName);
        if (!renamed.IsSuccess)
        {
            return (renamed.Error, []);
        }

        var me = new MeView(user.Id, user.Email, user.FirstName, user.LastName, await users.IsSystemAdminAsync(user.Id, cancellationToken), user.Language, user.TimeZone);
        if (renamed.Value == StateChange.Unchanged)
        {
            return (me, []);
        }

        await users.SaveChangesAsync(cancellationToken);
        var now = time.GetUtcNow();
        return (me, [new UserRenamedEvent(Guid.CreateVersion7(now), now, user.Id, user.Id)]);
    }
}
