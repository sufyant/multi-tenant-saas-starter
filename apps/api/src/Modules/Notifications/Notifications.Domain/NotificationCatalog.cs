using Notifications.Domain.Notifications;

namespace Notifications.Domain;

/// <summary>
/// Every notification type, each declared once (U1). A type is a template: its entry here, its texts in <c>packages/emails</c> (U2) and
/// one handler that hears its event. A type has the name of its event's audit operation (U9), and the channels the product fixes for it
/// (U4, U5). The OpenAPI document reads the types into the enum NotificationType, so a client knows every one. A notification keeps its type
/// as text, and a stored name never changes.
/// </summary>
public static class NotificationCatalog
{
    public const string MemberJoined = "member.joined";

    public const string MemberRemoved = "member.removed";

    public const string MemberRoleChanged = "member.role_changed";

    public const string TenantClosed = "tenant.closed";

    public const string TenantEntitlementOverrideRemoved = "tenant.entitlement_override_removed";

    public const string TenantEntitlementOverrideSet = "tenant.entitlement_override_set";

    /// <summary>
    /// The exception of U9: the acceptance of the platform's Owner invitation is Audit's <c>member.joined</c>, but the system admins hear
    /// of it, not the inviter, with texts of their own, so it keeps a name of its own.
    /// </summary>
    public const string TenantOwnerJoined = "tenant.owner_joined";

    public const string TenantPlanChanged = "tenant.plan_changed";

    public const string TenantPurged = "tenant.purged";

    public const string TenantReactivated = "tenant.reactivated";

    public const string TenantReopened = "tenant.reopened";

    public const string TenantSuspended = "tenant.suspended";

    /// <summary>The types whose name is no audit operation, each with its reason (U9).</summary>
    public static readonly IReadOnlyDictionary<string, string> NamedApart = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [TenantOwnerJoined] = "member.joined tells the inviter; the platform's Owner invitation tells the system admins instead.",
    };

    /// <summary>Every type, by its name.</summary>
    public static readonly IReadOnlyDictionary<string, NotificationKind> Kinds = new NotificationKind[]
    {
        // The platform's Make Owner names no person, as the tenant's Activity says "the platform": the actor is missing then. It leads to
        // the general settings, which show "Your role" to every role. A role is the member's access, so the email tells them too (U5).
        new(
            MemberRoleChanged,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            WebAndEmail(),
            [Tenant(), Person("actorName", optional: true), Role("oldRoleName"), Role("newRoleName")]),

        // The removed person may have no other tenant and so never see a bell, so the email tells them too (U5). Only a member
        // removes a member: the platform does so only in the user's own deletion, which tells nobody. It leads to the start page, which
        // opens what the person can still enter.
        new(
            MemberRemoved,
            NotificationBell.Personal,
            NotificationLinkKind.Start,
            WebAndEmail(),
            [Tenant(), Person("actorName")]),
        new(
            MemberJoined,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantMembers,
            WebOnly(),
            [Tenant(), Person("memberName"), Role("roleName")]),
        // The plan sets what the organisation may do, so the email tells its Owners too (U5).
        new(
            TenantPlanChanged,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            WebAndEmail(),
            [Tenant(), new("oldPlanName", NotificationValueKind.Text), new("newPlanName", NotificationValueKind.Text)]),

        // An override without an end has no expiry. An override leads to the page of its key (NotificationLinks).
        new(
            TenantEntitlementOverrideSet,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            WebOnly(),
            [Tenant(), Key(), new("value", NotificationValueKind.Entitlement), new("expiresAt", NotificationValueKind.Date, Optional: true)],
            LinkByKey: true),

        // The value is the plan's, which applies again.
        new(
            TenantEntitlementOverrideRemoved,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            WebOnly(),
            [Tenant(), Key(), new("value", NotificationValueKind.Entitlement)],
            LinkByKey: true),
        new(
            TenantOwnerJoined,
            NotificationBell.Platform,
            NotificationLinkKind.PlatformTenant,
            WebOnly(),
            [Tenant(), Person("ownerName")]),
        Status(TenantSuspended),
        Status(TenantReactivated),
        Status(TenantClosed, new NotificationValue("reopenableUntil", NotificationValueKind.Date)),
        Status(TenantReopened),
        new(
            TenantPurged,
            NotificationBell.Platform,
            NotificationLinkKind.PlatformTenant,
            WebAndEmail(),
            [Tenant()]),
    }.ToDictionary(kind => kind.Type, StringComparer.Ordinal);

    /// <summary>The entry of a stored or a sent type. A type the catalog does not know is a mistake of ours.</summary>
    public static NotificationKind Of(string type) =>
        Kinds.TryGetValue(type, out var kind) ? kind : throw new InvalidOperationException($"The notification catalog has no type {type}.");

    // A change of a tenant's status, to every system admin but the one who did it, in the platform's bell and by email (U5).
    private static NotificationKind Status(string type, params NotificationValue[] more) => new(
        type,
        NotificationBell.Platform,
        NotificationLinkKind.PlatformTenant,
        WebAndEmail(),
        [Tenant(), Person("actorName"), .. more]);

    private static NotificationValue Tenant() => new("tenantName", NotificationValueKind.Text);

    private static NotificationValue Person(string name, bool optional = false) => new(name, NotificationValueKind.Person, Optional: optional);

    private static NotificationValue Role(string name) => new(name, NotificationValueKind.Role);

    private static NotificationValue Key() => new(NotificationKind.KeyValue, NotificationValueKind.EntitlementKey);

    // Every type of the template is transactional, so the product chooses its channels (U4). A change of a person's access or of an
    // organisation's status also goes by email; team news goes to the bell only (U5).
    private static NotificationChannel[] WebOnly() => [NotificationChannel.Web];

    private static NotificationChannel[] WebAndEmail() => [NotificationChannel.Web, NotificationChannel.Email];
}
