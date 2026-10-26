using ControlPlane.Contracts.Users;
using ControlPlane.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Tenancy.Caching;

namespace ControlPlane.Infrastructure.Persistence.Users;

// Reads the users in control_plane.users, which belongs to no tenant (R6). The state of a user is read on every me and tenant request
// (A9), in the HTTP pipeline before any message is sent, and one read needs no transaction of its own. The cache keeps it for a minute at
// most (CachedReads.UserAccess). A deleted user is refused too:
// the identity provider deletes its user only after ControlPlane's deletion commits, and a session token lives on until it expires (A8).
// The names of a page of users are read in one query. A deleted user's copy is erased (A8), so they have no name and are marked deleted
// wherever an id points to them (K3). Public because Wolverine's generated code builds it (W1, W9).
public sealed class UserDirectory(ControlPlaneDbContext controlPlane, ReadCache cache) : IUserDirectory
{
    private static readonly Error Suspended = Error.Forbidden(ControlPlaneErrorCodes.UserSuspended, "Your account is suspended.");

    private static readonly Error Deleted = Error.Forbidden(ControlPlaneErrorCodes.UserDeleted, "Your account is deleted.");

    public async Task<Result> CheckActiveAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var state = await cache.ReadAsync(CachedReads.UserAccess, externalUserId, token => ReadAccessAsync(externalUserId, token), cancellationToken);
        return state switch
        {
            { Deleted: true } => Deleted,
            { Suspended: true } => Suspended,
            _ => Result.Success(),
        };
    }

    public async Task<IReadOnlyDictionary<Guid, UserName>> FindNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var users = await controlPlane.Users
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.DeletedAt })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            user => user.Id,
            user => new UserName(DisplayName(user.FirstName, user.LastName), user.DeletedAt is not null));
    }

    // The full name, else none: the identity provider may hold a user without a name, and the email address is not a name. A deleted
    // user's name is erased (A8).
    private static string? DisplayName(string? firstName, string? lastName) =>
        string.Join(' ', new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part))) is { Length: > 0 } name ? name : null;

    // A person who is not a user yet is neither deleted nor suspended.
    private async Task<UserAccess> ReadAccessAsync(string externalUserId, CancellationToken cancellationToken) =>
        await controlPlane.Users
            .Where(user => user.ExternalId == externalUserId)
            .Select(user => new UserAccess(user.DeletedAt != null, user.SuspendedAt != null))
            .SingleOrDefaultAsync(cancellationToken)
        ?? new UserAccess(Deleted: false, Suspended: false);
}
