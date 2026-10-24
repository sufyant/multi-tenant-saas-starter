using SharedKernel;

namespace ControlPlane.Contracts.SystemAdmins;

/// <summary>Who may pass the system door, and with which permissions.</summary>
public interface ISystemAdminDirectory
{
    /// <summary>
    /// The platform permissions the user holds behind the system door, or null when the door stays closed to them. The caller
    /// says whether the session verified a second factor; ControlPlane decides what that means (A6).
    /// </summary>
    Task<IReadOnlySet<string>?> FindPlatformPermissionsAsync(string externalUserId, bool secondFactorVerified, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a second factor verified this long ago is fresh enough for an operation that requires reverification, such as the grant
    /// and the revocation of a system admin; null means none was verified. The caller hands on the age; ControlPlane decides (A6).
    /// </summary>
    Result CheckReverification(TimeSpan? secondFactorAge);
}
