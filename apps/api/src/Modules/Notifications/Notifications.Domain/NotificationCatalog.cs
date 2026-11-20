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

    public const string TenantPaymentFailed = "tenant.payment_failed";

    /// <summary>
    /// The third exception of U9: the last warning before an unpaid tenant moves to the default plan (13.19, 3.18). Billing's grace starts
    /// when the provider's retries are over; no command does it, and the Activity log records no entry of it (12.12).
    /// </summary>
    public const string TenantPaymentGraceStarted = "tenant.payment_grace_started";

    public const string TenantMovedToDefaultPlan = "tenant.moved_to_default_plan";

    public const string TenantPriceChangeAnnounced = "tenant.price_change_announced";

    /// <summary>The words of <c>period</c> of <see cref="TenantPriceChangeAnnounced"/>: the tenant pays monthly or yearly.</summary>
    public const string Month = "month";

    public const string Year = "year";

    public const string UnknownCard = "unknown";

    public const string TenantPurged = "tenant.purged";

    /// <summary>The fourth exception of U9: a use first took a meter's count to 80% of its quota (13.22). A use is a count, not a command.</summary>
    public const string TenantMeterNearlyUsed = "tenant.meter_nearly_used";

    /// <summary>A use first took a meter's count to its quota (13.23).</summary>
    public const string TenantMeterUsedUp = "tenant.meter_used_up";

    /// <summary>A use first took the month's extra to 80% of the spending cap (13.40).</summary>
    public const string TenantSpendingCapNearlyReached = "tenant.spending_cap_nearly_reached";

    /// <summary>A use reached the spending cap (13.24).</summary>
    public const string TenantSpendingCapReached = "tenant.spending_cap_reached";

    public const string TenantPurgedForOwners = "tenant.purged_for_owners";

    public const string TenantReactivated = "tenant.reactivated";

    public const string TenantReactivatedForOwners = "tenant.reactivated_for_owners";

    public const string TenantReopened = "tenant.reopened";

    public const string TenantReopenedForOwners = "tenant.reopened_for_owners";

    public const string TenantSuspended = "tenant.suspended";

    public const string TenantSuspendedForOwners = "tenant.suspended_for_owners";

    public const string TermsVersionCreated = "terms_version.created";

    /// <summary>The release flags on for everyone for 30 days, which the weekly cleanup tells the system admins of (3.07).</summary>
    public const string FeatureFlagsReadyToRemove = "feature_flags.ready_to_remove";

    /// <summary>The flags off for every organisation for 30 days, which the weekly cleanup tells the system admins of (3.07).</summary>
    public const string FeatureFlagsUnused = "feature_flags.unused";

    /// <summary>The words of <c>nextStep</c> of <see cref="TermsVersionCreated"/>: the person accepts at their next sign-in, or does nothing.</summary>
    public const string AcceptAtSignIn = "accept_at_sign_in";

    public const string NothingToDo = "nothing_to_do";

    /// <summary>
    /// The second exception of U9: the export's completion is the worker's, not a command, so it writes no audit entry and has no operation
    /// to take its name from.
    /// </summary>
    public const string UserDataExportReady = "user.data_export_ready";

    /// <summary>
    /// The card brands as the provider names them (Stripe, "Card object", <c>brand</c>), the words of <c>cardBrand</c>. A brand that is not
    /// in the list is <see cref="UnknownCard"/>, which the texts write as "the card".
    /// </summary>
    public static readonly IReadOnlyList<string> CardBrands =
        ["amex", "cartes_bancaires", "diners", "discover", "eftpos_au", "interac", "jcb", "link", "mastercard", "unionpay", "visa", UnknownCard];

    /// <summary>The types whose name is no audit operation, each with its reason (U9).</summary>
    public static readonly IReadOnlyDictionary<string, string> NamedApart = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [TenantOwnerJoined] = "member.joined tells the inviter; the platform's Owner invitation tells the system admins instead.",
        [UserDataExportReady] = "The worker completes an export; no command does, so no audit operation records it.",
        [TenantPaymentGraceStarted] = "Billing's grace starts when the provider's retries are over; no command does, and the Activity log has no entry of it.",
        [FeatureFlagsReadyToRemove] = FlagsApart,
        [FeatureFlagsUnused] = FlagsApart,
        [TenantSuspendedForOwners] = OwnersApart,
        [TenantReactivatedForOwners] = OwnersApart,
        [TenantClosedForOwners] = OwnersApart,
        [TenantReopenedForOwners] = OwnersApart,
        [TenantPurgedForOwners] = OwnersApart,
        [TenantMeterNearlyUsed] = UsageApart,
        [TenantMeterUsedUp] = UsageApart,
        [TenantSpendingCapNearlyReached] = UsageApart,
        [TenantSpendingCapReached] = UsageApart,
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
        // The plan sets what the organisation may do, so the email tells its Owners too (U5). A shorter Activity log period names the new
        // period and the moment the older entries go; the two are there together or not at all (K14).
        new(
            TenantPlanChanged,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantSettings,
            OrganisationPlan,
            WebAndEmail(),
            [
                Tenant(),
                new("oldPlanName", NotificationValueKind.Text),
                new("newPlanName", NotificationValueKind.Text),
                new("retentionDays", NotificationValueKind.Days, Optional: true),
                new("retentionGoesAt", NotificationValueKind.Date, Optional: true, With: "retentionDays"),
            ]),

        // Billing tells the members who manage it, in the tenant's bell and by email, as the organisation's plan depends on the payment
        // (U5). Each leads to Billing. A card the provider did not name, or an invoice it no longer holds, is missing: the texts then say
        // the payment without it.
        new(
            TenantPaymentFailed,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantBilling,
            OrganisationPlan,
            WebAndEmail(),
            [
                Tenant(),
                Money("amountDue"),
                new("failedAt", NotificationValueKind.Date),
                new("nextAttemptAt", NotificationValueKind.Date),
                .. Card(),
            ]),
        new(
            TenantPaymentGraceStarted,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantBilling,
            OrganisationPlan,
            WebAndEmail(),
            [
                Tenant(),
                new("defaultPlanName", NotificationValueKind.Text),
                new("graceEndsAt", NotificationValueKind.Date),
                new("attempts", NotificationValueKind.Attempts),
                new("failedAt", NotificationValueKind.Date),
                .. Card(),
            ]),
        new(
            TenantMovedToDefaultPlan,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantBilling,
            OrganisationPlan,
            WebAndEmail(),
            [
                Tenant(),
                new("planName", NotificationValueKind.Text),
                new("defaultPlanName", NotificationValueKind.Text),
                new("movedAt", NotificationValueKind.Date),
                Money("amountDue", optional: true),
            ]),

        // A price change names the fees that change: the fee per person, the base fee or both, so at least one old fee is there. The
        // amounts are of the interval the tenant pays (period), and the totals are for the people who take a seat at the notice.
        new(
            TenantPriceChangeAnnounced,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantBilling,
            OrganisationPlan,
            WebAndEmail(),
            [
                Tenant(),
                new("planName", NotificationValueKind.Text),
                new("movesAt", NotificationValueKind.Date),
                new("period", NotificationValueKind.Word, Words: [Month, Year]),
                Money("perPerson"),
                Money("oldPerPerson", optional: true),
                Money("base"),
                Money("oldBase", optional: true),
                new("people", NotificationValueKind.People),
                Money("newTotal"),
                Money("oldTotal"),
            ]),

        // A meter's use tells the members who manage billing, in the tenant's bell and by email, as the extra costs the organisation money
        // or its use stops (U5; E10). The price is there only for a meter that charges its extra, and goes with its unit. Each leads to
        // Billing until the web has its Usage page (40c-frontend).
        new(
            TenantMeterNearlyUsed,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantBilling,
            OrganisationPlan,
            WebAndEmail(),
            [Tenant(), Meter(), Count("used"), Count("quota"), new("resetsAt", NotificationValueKind.Day), .. UnitPrice()]),
        new(
            TenantMeterUsedUp,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantBilling,
            OrganisationPlan,
            WebAndEmail(),
            [Tenant(), Meter(), Count("quota"), new("resetsAt", NotificationValueKind.Day), .. UnitPrice()]),
        new(
            TenantSpendingCapNearlyReached,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantBilling,
            OrganisationPlan,
            WebAndEmail(),
            [Tenant(), Money("cap"), Money("extraAmount"), new("resetsAt", NotificationValueKind.Day)]),
        new(
            TenantSpendingCapReached,
            NotificationBell.Tenant,
            NotificationLinkKind.TenantBilling,
            OrganisationPlan,
            WebAndEmail(),
            [Tenant(), Meter(), Money("cap"), new("resetsAt", NotificationValueKind.Day)]),

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

        // A new terms version that emails the users tells every user who may accept, by email only: Home shows its notice, or the gate
        // asks for the acceptance, from the terms read, not from the bell (docs/flows/terms-of-use.md). The terms of the person's account
        // change, so the email goes (U5). nextStep says whether the person accepts at their next sign-in.
        new(
            TermsVersionCreated,
            NotificationBell.Personal,
            NotificationLinkKind.Start,
            YourAccount,
            EmailOnly(),
            [
                new("effectiveDate", NotificationValueKind.Day),
                new("url", NotificationValueKind.Text),
                new("nextStep", NotificationValueKind.Word, Words: [AcceptAtSignIn, NothingToDo]),
                new("summary", NotificationValueKind.Text, Optional: true),
            ]),

        // The weekly cleanup of the feature flags tells every system admin, in the platform's bell only, as the platform tells them of an
        // organisation's new Owner: the product fixes its channel, and no access changes, so no email goes (U5, G6). It leads to the
        // platform's Feature flags.
        FeatureFlagCleanup(FeatureFlagsReadyToRemove),
        FeatureFlagCleanup(FeatureFlagsUnused),
    }.ToDictionary(kind => kind.Type, StringComparer.Ordinal);

    // The areas of the list, each a word of notificationWords.area: a member's news, a change of the reader's own role or membership, the
    // plan and its values, the platform's news of an organisation, and the reader's own account.
    private const string Members = "members";

    private const string YourAccess = "your_access";

    private const string OrganisationPlan = "organisation_plan";

    private const string Organisations = "organisations";

    private const string YourAccount = "your_account";

    private const string FeatureFlags = "feature_flags";

    // The weekly cleanup of the flags is the worker's, not a command, so it writes no audit entry and has no operation to take its name from.
    private const string FlagsApart = "The worker's weekly cleanup finds the flags; no command does, so no audit operation records it.";

    // The status types of the Owners share the event of the platform's type, which tells the system admins with other texts.
    private const string OwnersApart = "The same event tells the system admins in the platform's bell; the organisation's Owners hear of it with texts of their own.";

    private const string UsageApart = "A use of a meter is a count, not a command, so no audit operation records it (E8).";

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

    private static NotificationKind FeatureFlagCleanup(string type) => new(
        type,
        NotificationBell.Platform,
        NotificationLinkKind.PlatformFeatureFlags,
        FeatureFlags,
        WebOnly(),
        [new("count", NotificationValueKind.FeatureFlags)]);

    private static NotificationValue Tenant() => new(NotificationKind.TenantValue, NotificationValueKind.Text);

    private static NotificationValue Person(string name, bool optional = false) => new(name, NotificationValueKind.Person, Optional: optional);

    private static NotificationValue Role(string name) => new(name, NotificationValueKind.Role);

    private static NotificationValue Money(string name, bool optional = false) => new(name, NotificationValueKind.Money, Optional: optional);

    // The card the provider tried: its brand and its last four digits, there together or not at all.
    private static NotificationValue[] Card() =>
    [
        new("cardBrand", NotificationValueKind.Word, Words: CardBrands, Optional: true),
        new("cardLast4", NotificationValueKind.Text, Optional: true, With: "cardBrand"),
    ];

    private static NotificationValue Meter() => new("meter", NotificationValueKind.Meter);

    private static NotificationValue Count(string name) => new(name, NotificationValueKind.Count);

    // The price of a charge meter's extra: its amount for each unit size, there together or not at all.
    private static NotificationValue[] UnitPrice() =>
    [
        Money("unitPrice", optional: true),
        new("unitSize", NotificationValueKind.Count, Optional: true, With: "unitPrice"),
    ];

    private static NotificationValue Key() => new(NotificationKind.KeyValue, NotificationValueKind.EntitlementKey);

    // Every type of the template is transactional, so the product chooses its channels (U4). A change of a person's access or of an
    // organisation's status also goes by email; team news goes to the bell only (U5).
    private static NotificationChannel[] WebOnly() => [NotificationChannel.Web];

    private static NotificationChannel[] WebAndEmail() => [NotificationChannel.Web, NotificationChannel.Email];

    private static NotificationChannel[] EmailOnly() => [NotificationChannel.Email];
}
