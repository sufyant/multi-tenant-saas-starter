using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Users;

/// <summary>
/// A person's names as the catalog holds them now, resolved when a screen reads them, as the audit names are (K3). A deleted user has no
/// names and is marked deleted, so the screen says "Deleted user" in its own language. It holds no email address.
/// </summary>
public sealed record PersonName(string? FirstName, string? LastName, bool Deleted)
{
    internal static PersonName Of(User user) => new(user.FirstName, user.LastName, user.DeletedAt is not null);
}
