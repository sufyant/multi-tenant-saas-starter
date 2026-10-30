using ControlPlane.Contracts;

namespace Notifications.Application.Notifications;

/// <summary>
/// The permissions of the bells, from ControlPlane's one list, for the endpoints, which see no other module's Contracts (section 2), and
/// the permissions that decide which categories can reach a user (U5).
/// </summary>
public static class NotificationPermissions
{
    /// <summary>Every member reads the tenant's bell: every built-in role holds this permission, and every custom role must (A3).</summary>
    public const string TenantBell = Permissions.TenantProfileRead;

    /// <summary>Every platform notification is about a tenant, which every system admin reads.</summary>
    public const string PlatformBell = Permissions.PlatformTenantsRead;

    /// <summary>A member who invites hears that an invitation was accepted.</summary>
    public const string InvitesMembers = Permissions.TenantInvitationsCreate;

    /// <summary>An Owner hears that the tenant's plan changed (A1).</summary>
    public const string ManagesOwners = Permissions.TenantOwnersManage;
}
