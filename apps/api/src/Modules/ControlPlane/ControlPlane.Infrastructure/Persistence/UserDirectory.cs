using ControlPlane.Contracts;
using ControlPlane.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence;

// Reads the user's state in control_plane.users, which belongs to no tenant (R6), on every me and tenant request (A9). It runs in the
// HTTP pipeline before any message is sent, and one read needs no transaction of its own. A deleted user is refused too: the identity
// provider deletes its user only after the catalog's deletion commits, and a session token lives on until it expires (A8).
internal sealed class UserDirectory(ControlPlaneDbContext controlPlane) : IUserDirectory
{
    private static readonly Error Suspended = Error.Forbidden(ControlPlaneErrorCodes.UserSuspended, "Your account is suspended.");

    private static readonly Error Deleted = Error.Forbidden(ControlPlaneErrorCodes.UserDeleted, "Your account is deleted.");

    public async Task<Result> CheckActiveAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var state = await controlPlane.Users
            .Where(user => user.ExternalId == externalUserId)
            .Select(user => new { Deleted = user.DeletedAt != null, Suspended = user.SuspendedAt != null })
            .SingleOrDefaultAsync(cancellationToken);
        return state switch
        {
            { Deleted: true } => Deleted,
            { Suspended: true } => Suspended,
            _ => Result.Success(),
        };
    }
}
