namespace Audit.Domain;

/// <summary>
/// The operation of every audit entry, each declared once (K3). The OpenAPI document reads these constants into the enum
/// AuditOperation, so a client knows every operation. An entry keeps its operation as text, and a stored name never changes.
/// </summary>
public static class AuditOperations
{
    /// <summary>A member exported the Activity log to a file. The screen's name of the log is the operation's.</summary>
    public const string ActivityLogExported = "activity_log.exported";

    /// <summary>The first deploy whose code defines a feature flag stored it; the platform acted.</summary>
    public const string FeatureFlagAddedToCode = "feature_flag.added_to_code";

    /// <summary>A system admin turned a feature flag off for everyone else, its third rule.</summary>
    public const string FeatureFlagEveryoneElseOff = "feature_flag.everyone_else_off";

    /// <summary>A system admin turned a feature flag on for everyone else, its third rule.</summary>
    public const string FeatureFlagEveryoneElseOn = "feature_flag.everyone_else_on";

    public const string FeatureFlagPercentageSet = "feature_flag.percentage_set";

    /// <summary>The first deploy whose code retires a feature flag archived it; the platform acted.</summary>
    public const string FeatureFlagRemovedFromCode = "feature_flag.removed_from_code";

    /// <summary>A system admin listed an organisation in a feature flag's first rule.</summary>
    public const string FeatureFlagTenantListed = "feature_flag.tenant_listed";

    public const string FeatureFlagTenantUnlisted = "feature_flag.tenant_unlisted";

    public const string InvitationRevoked = "invitation.revoked";

    public const string MemberInvited = "member.invited";

    /// <summary>A member exported the members to a file.</summary>
    public const string MembersExported = "members.exported";

    public const string MemberJoined = "member.joined";

    /// <summary>A member left the tenant by themselves.</summary>
    public const string MemberLeft = "member.left";

    public const string MemberRemoved = "member.removed";

    public const string MemberRoleChanged = "member.role_changed";

    public const string PlanCreated = "plan.created";

    public const string PlanUpdated = "plan.updated";

    public const string RoleCreated = "role.created";

    public const string RoleDeleted = "role.deleted";

    public const string RoleUpdated = "role.updated";

    public const string SystemAdminGranted = "system_admin.granted";

    public const string SystemAdminRevoked = "system_admin.revoked";

    public const string TenantClosed = "tenant.closed";

    public const string TenantEntitlementOverrideRemoved = "tenant.entitlement_override_removed";

    public const string TenantEntitlementOverrideSet = "tenant.entitlement_override_set";

    public const string TenantCreated = "tenant.created";

    public const string TenantOwnerInvited = "tenant.owner_invited";

    public const string TenantOwnerMade = "tenant.owner_made";

    public const string TenantReactivated = "tenant.reactivated";

    public const string TenantPlanChanged = "tenant.plan_changed";

    /// <summary>A member cancelled the tenant's subscription: it moves to the default plan at the end of the paid period (B9).</summary>
    public const string TenantPlanCancelled = "tenant.plan_cancelled";

    /// <summary>A member undid the tenant's cancellation: it stays on its plan.</summary>
    public const string TenantPlanCancellationUndone = "tenant.plan_cancellation_undone";

    /// <summary>A member scheduled a move to another plan or interval at the end of the paid period (B9).</summary>
    public const string TenantPlanChangeScheduled = "tenant.plan_change_scheduled";

    /// <summary>A member undid the move they scheduled: the tenant stays on its plan.</summary>
    public const string TenantPlanChangeUndone = "tenant.plan_change_undone";

    /// <summary>A payment of the tenant's subscription failed; the provider retries it (B11).</summary>
    public const string TenantPaymentFailed = "tenant.payment_failed";

    /// <summary>The tenant moved to the default plan: a payment failed and its grace ended (B11).</summary>
    public const string TenantMovedToDefaultPlan = "tenant.moved_to_default_plan";

    /// <summary>A system admin scheduled the move of a plan's payers to its new prices (B13); in the platform's log only.</summary>
    public const string PlanPriceChangeScheduled = "plan.price_change_scheduled";

    /// <summary>The tenant was told that its plan's price changes (B13); in the tenant's log only.</summary>
    public const string TenantPriceChangeAnnounced = "tenant.price_change_announced";

    /// <summary>A member who manages billing set, changed or removed the tenant's billing email (B14); in the tenant's log only.</summary>
    public const string TenantBillingEmailChanged = "tenant.billing_email_changed";

    /// <summary>A system admin marked the tenant Contract: it is billed outside the app (B12).</summary>
    public const string TenantContractStarted = "tenant.contract_started";

    /// <summary>A system admin ended the tenant's Contract: it pays by card in the app (B12).</summary>
    public const string TenantContractEnded = "tenant.contract_ended";

    public const string TenantPurgeWarned = "tenant.purge_warned";

    public const string TenantPurged = "tenant.purged";

    public const string TenantRenamed = "tenant.renamed";

    public const string TenantReopened = "tenant.reopened";

    public const string TenantSuspended = "tenant.suspended";

    public const string TermsVersionCancelled = "terms_version.cancelled";

    public const string TermsVersionCreated = "terms_version.created";

    public const string TermsVersionUpdated = "terms_version.updated";

    public const string UserDataExportDownloaded = "user.data_export_downloaded";

    public const string UserDataExportRequested = "user.data_export_requested";

    public const string UserDeleted = "user.deleted";

    public const string UserDeletionRequested = "user.deletion_requested";

    public const string UserEmailChanged = "user.email_changed";

    /// <summary>
    /// Retired in step 35: the notification preferences are gone, so no command writes it. Stored entries keep it, and the read names their
    /// operation from this list (K3).
    /// </summary>
    public const string UserEmailPreferencesChanged = "user.email_preferences_changed";

    public const string UserPreferencesChanged = "user.preferences_changed";

    public const string UserReactivated = "user.reactivated";

    public const string UserRenamed = "user.renamed";

    public const string UserSuspended = "user.suspended";
}
