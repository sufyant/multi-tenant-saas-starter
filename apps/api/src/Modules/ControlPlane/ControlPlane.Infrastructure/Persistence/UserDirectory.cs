using ControlPlane.Contracts;
using ControlPlane.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence;

// Reads the user's state in control_plane.users, which belongs to no tenant (R6), on every me and tenant request (A9). It runs in the
// HTTP pipeline before any message is sent, and one read needs no transaction of its own.
internal sealed class UserDirectory(ControlPlaneDbContext controlPlane) : IUserDirectory
{
    private static readonly Error Suspended = Error.Forbidden(ControlPlaneErrorCodes.UserSuspended, "Your account is suspended.");

    public async Task<Result> CheckActiveAsync(string externalUserId, CancellationToken cancellationToken) =>
        await controlPlane.Users.AnyAsync(user => user.ExternalId == externalUserId && user.SuspendedAt != null, cancellationToken)
            ? Suspended
            : Result.Success();
}
