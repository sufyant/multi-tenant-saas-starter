namespace ControlPlane.Domain.Tenants;

/// <summary>
/// Who invited, as the accept screen names them: a user, or the platform when a system admin invited. The screen never sees a system
/// admin's name.
/// </summary>
internal enum InviterKind
{
    User,
    Platform,
}
