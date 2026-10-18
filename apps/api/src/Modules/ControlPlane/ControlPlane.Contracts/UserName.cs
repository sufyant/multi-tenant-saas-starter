namespace ControlPlane.Contracts;

/// <summary>
/// A user's real name: the full name, or null when the user has none or was deleted; never the email address. A screen labels a user
/// without a name in its own language, from <paramref name="Deleted"/>.
/// </summary>
public sealed record UserName(string? DisplayName, bool Deleted);
