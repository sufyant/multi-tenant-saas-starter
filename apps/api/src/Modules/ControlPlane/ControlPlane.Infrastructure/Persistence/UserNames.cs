using ControlPlane.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ControlPlane.Infrastructure.Persistence;

// Reads the copy of the users' profiles in control_plane.users, which belongs to no tenant (R6), for a page of ids in one query. A
// deleted user's copy is erased (A8), so their name is "Deleted user" wherever an id points to them (K3).
internal sealed class UserNames(ControlPlaneDbContext controlPlane) : IUserNames
{
    private const string Unnamed = "Unnamed user";

    public async Task<IReadOnlyDictionary<Guid, UserName>> FindAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var users = await controlPlane.Users
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.Email, user.DeletedAt })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            user => user.Id,
            user => user.DeletedAt is not null ? new UserName(UserName.Deleted, null) : new UserName(DisplayName(user.FirstName, user.LastName, user.Email), user.Email));
    }

    // The full name, else the email address, else a placeholder: the identity provider may hold a user without a name.
    private static string DisplayName(string? firstName, string? lastName, string? email) =>
        string.Join(' ', new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part))) is { Length: > 0 } name
            ? name
            : email ?? Unnamed;
}
