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

    public const string TenantClosedForOwners = "tenant.closed_for_owners";

    public const string TenantEntitlementOverrideRemoved = "tenant.entitlement_override_removed";

    public const string TenantEntitlementOverrideSet = "tenant.entitlement_override_set";

    /// <summary>
    /// The exception of U9: the acceptance of the platform's Owner invitation is Audit's <c>member.joined</c>, but the system admins hear
    /// of it, not the inviter, with texts of their own, so it keeps a name of its own.
    /// </summary>
    public const string TenantOwnerJoined = "tenant.owner_joined";

    public const string TenantPlanChanged = "tenant.plan_changed";

    public const string TenantPurged = "tenant.purged";

    public const string TenantPurgedForOwners = "tenant.purged_for_owners";

    public const string TenantReactivated = "tenant.reactivated";

    public const string TenantReactivatedForOwners = "tenant.reactivated_for_owners";

    public const string TenantReopened = "tenant.reopened";

    public const string TenantReopenedForOwners = "tenant.reopened_for_owners";

    public const string TenantSuspended = "tenant.suspended";

    public const string TenantSuspendedForOwners = "tenant.suspended_for_owners";

    /// <summary>
    /// The second exception of U9: the export's completion is the worker's, not a command, so it writes no audit entry and has no operation
    /// to take its name from.
    /// </summary>
    public const string UserDataExportReady = "user.data_export_ready";

    /// <summary>The types whose name is no audit operation, each with its reason (U9).</summary>
    public static readonly IReadOnlyDictionary<string, string> NamedApart = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [TenantOwnerJoined] = "member.joined tells the inviter; the platform's Owner invitation tells the system admins instead.",
        [UserDataExportReady] = "The worker completes an export; no command does, so no audit operation records it.",
        [TenantSuspendedForOwners] = OwnersApart,
        [TenantReactivatedForOwners] = OwnersApart,
        [TenantClosedForOwners] = OwnersApart,
        [TenantReopenedForOwners] = OwnersApart,
        [TenantPurgedForOwners] = OwnersApart,
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
            YourAccess,
            WebAndEmail(),
            [Tenant(), Person("actorName", optional: true), Role("oldRoleName"), Role("newRoleName")],
            Actor: "actorName"),

        // The removed person may have no other tenant and so never see a bell, so the email tells them too (U5). Only a member
        // removes a member: the platform does so only in the user's own deletion, which tells nobody. It leads to the start page, which
        // opens what the person can still enter.
        new(
            MemberRemoved,
            NotificationBell.Personal,
            NotificationLinkKind.Start,
            YourAccess,
            WebAndEmail(),
            [Tenant(), Person("actorName")],
            Actor: "actorName"),
        new(
            MemberJoined,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantMembers,
            Members,
            WebOnly(),
            [Tenant(), Person("memberName"), Role("roleName")],
            Actor: "memberName"),
        // The plan sets what the organisation may do, so the email tells its Owners too (U5).
        new(
            TenantPlanChanged,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            OrganisationPlan,
            WebAndEmail(),
            [Tenant(), new("oldPlanName", NotificationValueKind.Text), new("newPlanName", NotificationValueKind.Text)]),

        // An override without an end has no expiry. An override leads to the page of its key (NotificationLinks).
        new(
            TenantEntitlementOverrideSet,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            OrganisationPlan,
            WebOnly(),
            [Tenant(), Key(), new("value", NotificationValueKind.Entitlement), new("expiresAt", NotificationValueKind.Date, Optional: true)],
            LinkByKey: true),

        // The value is the plan's, which applies again.
        new(
            TenantEntitlementOverrideRemoved,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            OrganisationPlan,
            WebOnly(),
            [Tenant(), Key(), new("value", NotificationValueKind.Entitlement)],
            LinkByKey: true),
        new(
            TenantOwnerJoined,
            NotificationBell.Platform,
            NotificationLinkKind.PlatformTenant,
            Organisations,
            WebOnly(),
            [Tenant(), Person("ownerName")],
            Actor: "ownerName"),
        Status(TenantSuspended),
        Status(TenantReactivated),
        Status(TenantClosed, new NotificationValue("reopenableUntil", NotificationValueKind.Date)),
        Status(TenantReopened),
        new(
            TenantPurged,
            NotificationBell.Platform,
            NotificationLinkKind.PlatformTenant,
            Organisations,
            WebAndEmail(),
            [Tenant()]),

        // The organisation's Owners hear of its status in the personal bell, because the tenant door may refuse them, and by email (U5). The
        // platform acted, so no system admin is named. A suspended, closed or purged organisation cannot be opened, so it leads to the start
        // page; a reactivated or reopened one leads into the organisation, so that notification keeps the tenant it tells of.
        OwnersStatus(TenantSuspendedForOwners, NotificationLinkKind.Start),
        OwnersStatus(TenantReactivatedForOwners, NotificationLinkKind.TenantSettings),
        OwnersStatus(TenantClosedForOwners, NotificationLinkKind.Start, new NotificationValue("reopenableUntil", NotificationValueKind.Date)),
        OwnersStatus(TenantReopenedForOwners, NotificationLinkKind.TenantSettings),
        OwnersStatus(TenantPurgedForOwners, NotificationLinkKind.Start),

        // A person's data is ready to download: the person may not open the app soon, so the email tells them too (U5). It leads to Account
        // and security, never to the file.
        new(
            UserDataExportReady,
            NotificationBell.Personal,
            NotificationLinkKind.AccountSecurity,
            YourAccount,
            WebAndEmail(),
            [new("expiresAt", NotificationValueKind.Date)]),
    }.ToDictionary(kind => kind.Type, StringComparer.Ordinal);

    // The areas of the list, each a word of notificationWords.area: a member's news, a change of the reader's own role or membership, the
    // plan and its values, the platform's news of an organisation, and the reader's own account.
    private const string Members = "members";

    private const string YourAccess = "your_access";

    private const string OrganisationPlan = "organisation_plan";

    private const string Organisations = "organisations";

    private const string YourAccount = "your_account";

    // The status types of the Owners share the event of the platform's type, which tells the system admins with other texts.
    private const string OwnersApart = "The same event tells the system admins in the platform's bell; the organisation's Owners hear of it with texts of their own.";

    /// <summary>The entry of a stored or a sent type. A type the catalog does not know is a mistake of ours.</summary>
    public static NotificationKind Of(string type) =>
        Kinds.TryGetValue(type, out var kind) ? kind : throw new InvalidOperationException($"The notification catalog has no type {type}.");

    // A change of a tenant's status, to every system admin but the one who did it, in the platform's bell and by email (U5).
    private static NotificationKind Status(string type, params NotificationValue[] more) => new(
        type,
        NotificationBell.Platform,
        NotificationLinkKind.PlatformTenant,
        Organisations,
        WebAndEmail(),
        [Tenant(), Person("actorName"), .. more],
        Actor: "actorName");

    // A change of a tenant's status, to its Owners, in the personal bell and by email (U5). Their access to the organisation changes.
    private static NotificationKind OwnersStatus(string type, NotificationLinkKind link, params NotificationValue[] more) => new(
        type,
        NotificationBell.Personal,
        link,
        YourAccess,
        WebAndEmail(),
        [Tenant(), .. more]);

    private static NotificationValue Tenant() => new(NotificationKind.TenantValue, NotificationValueKind.Text);

    private static NotificationValue Person(string name, bool optional = false) => new(name, NotificationValueKind.Person, Optional: optional);

    private static NotificationValue Role(string name) => new(name, NotificationValueKind.Role);

    private static NotificationValue Key() => new(NotificationKind.KeyValue, NotificationValueKind.EntitlementKey);

    // Every type of the template is transactional, so the product chooses its channels (U4). A change of a person's access or of an
    // organisation's status also goes by email; team news goes to the bell only (U5).
    private static NotificationChannel[] WebOnly() => [NotificationChannel.Web];

    private static NotificationChannel[] WebAndEmail() => [NotificationChannel.Web, NotificationChannel.Email];
}
