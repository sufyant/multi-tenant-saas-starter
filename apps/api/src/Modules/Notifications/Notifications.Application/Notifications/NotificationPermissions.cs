using ControlPlane.Contracts;

namespace Notifications.Application.Notifications;

/// <summary>
/// The permissions of the bells, from ControlPlane's one list, for the endpoints, which see no other module's Contracts (section 2).
/// </summary>
public static class NotificationPermissions
{
    /// <summary>Every member reads the tenant's bell: every built-in role holds this permission, and every custom role must (A3).</summary>
    public const string TenantBell = Permissions.TenantProfileRead;

    /// <summary>Every platform notification is about a tenant, which every system admin reads.</summary>
    public const string PlatformBell = Permissions.PlatformTenantsRead;
}
