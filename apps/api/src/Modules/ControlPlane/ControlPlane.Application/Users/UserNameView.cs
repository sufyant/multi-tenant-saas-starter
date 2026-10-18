using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Users;

/// <summary>
/// A person's names as they are stored now, resolved when a screen reads them, as the audit names are (K3). A deleted user has no
/// names and is marked deleted, so the screen says "Deleted user" in its own language. It holds no email address.
/// </summary>
public sealed record UserNameView(string? FirstName, string? LastName, bool Deleted)
{
    internal static UserNameView Of(User user) => new(user.FirstName, user.LastName, user.DeletedAt is not null);
}
