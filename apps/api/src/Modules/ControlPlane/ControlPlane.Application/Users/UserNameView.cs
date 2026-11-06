using ControlPlane.Domain.Users;

namespace ControlPlane.Application.Users;

/// <summary>
/// A person's name as it is stored now, resolved when a screen reads it, as the audit names are (K3). A deleted user has no
/// name and is marked deleted, so the screen says "Deleted user" in its own language. It holds no email address.
/// </summary>
public sealed record UserNameView(string? Name, bool Deleted)
{
    internal static UserNameView Of(User user) => new(user.Name, user.DeletedAt is not null);

    internal static UserNameView Of(UserProfile profile) => new(profile.Name, profile.Deleted);
}
