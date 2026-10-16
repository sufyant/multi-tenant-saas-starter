using ControlPlane.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence;

// Reads the copy of the users' profiles in control_plane.users, which belongs to no tenant (R6), for a page of ids in one query. A
// deleted user's copy is erased (A8), so they have no name and are marked deleted wherever an id points to them (K3). Public only so that
// Wolverine's generated code builds it for Audit's handler on this module's DbContext, in that DbContext's transaction (W1, W9).
public sealed class UserNames(ControlPlaneDbContext controlPlane) : IUserNames
{
    public async Task<IReadOnlyDictionary<Guid, UserName>> FindAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
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
}
