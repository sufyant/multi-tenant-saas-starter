namespace Notifications.Domain.Notifications;

/// <summary>
/// Who a category can reach (U5), so the preferences list it only for them. A tenant permission is named here by its meaning: the
/// application reads its name from ControlPlane's one list, which this project may not reference (section 2, D1).
/// </summary>
public enum NotificationAudience
{
    /// <summary>Every user.</summary>
    Everyone,

    /// <summary>A member who may invite people in some tenant: <c>tenant.invitations.create</c>.</summary>
    TenantInviters,

    /// <summary>A member who manages the Owners of some tenant: <c>tenant.owners.manage</c>.</summary>
    TenantOwnerManagers,

    /// <summary>A system admin.</summary>
    SystemAdmins,
}
