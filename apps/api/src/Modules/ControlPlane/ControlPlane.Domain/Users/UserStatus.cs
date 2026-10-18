namespace ControlPlane.Domain.Users;

/// <summary>The status of a user, which <see cref="User.StatusOf"/> derives from the user's moments.</summary>
internal enum UserStatus
{
    Active,
    Suspended,
    Deleted,
}
