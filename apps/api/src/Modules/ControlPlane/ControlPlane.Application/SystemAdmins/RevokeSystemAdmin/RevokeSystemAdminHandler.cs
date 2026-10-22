using ControlPlane.Application.Users;
using ControlPlane.Contracts.SystemAdmins;
using ControlPlane.Domain;
using ControlPlane.Domain.SystemAdmins;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.SystemAdmins.RevokeSystemAdmin;

// The user row is locked first, then the grant, as a grant locks them (K6a). Root is read from the configuration's list now, with the
// user's address (SystemDoor.IsRoot); nothing stores it. Rejections come first (W7). The door reads the system admin list on each request,
// so the revoked user is refused on their next platform request.
public static class RevokeSystemAdminHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(
        RevokeSystemAdminCommand command,
        IUserRepository users,
        SystemAdminOptions systemAdmins,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.UserId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, []);
        }

        if (await users.FindSystemAdminForUpdateAsync(user.Id, cancellationToken) is not { } systemAdmin)
        {
            return (Error.NotFound(ControlPlaneErrorCodes.SystemAdminNotFound, "The user is no system admin."), []);
        }

        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        var revocable = systemAdmin.CheckRevoke(actor, SystemDoor.IsRoot(user.Email, systemAdmins.Roots));
        if (!revocable.IsSuccess)
        {
            return (revocable, []);
        }

        users.RemoveSystemAdmin(systemAdmin);
        await users.SaveChangesAsync(cancellationToken);
        var now = time.GetUtcNow();
        return (Result.Success(), [new SystemAdminRevokedEvent(Guid.CreateVersion7(now), now, user.Id, actor)]);
    }
}
