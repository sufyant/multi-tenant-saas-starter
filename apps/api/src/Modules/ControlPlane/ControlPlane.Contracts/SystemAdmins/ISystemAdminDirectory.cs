namespace ControlPlane.Contracts.SystemAdmins;

/// <summary>Who may pass the system door, and with which permissions.</summary>
public interface ISystemAdminDirectory
{
    /// <summary>
    /// The platform permissions the user holds behind the system door, or null when the door stays closed to them. The caller
    /// says whether the session verified a second factor; ControlPlane decides what that means (A6).
    /// </summary>
    Task<IReadOnlySet<string>?> FindPlatformPermissionsAsync(string externalUserId, bool secondFactorVerified, CancellationToken cancellationToken);
}
