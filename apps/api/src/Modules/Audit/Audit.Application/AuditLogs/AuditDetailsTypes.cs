using System.Text.Json;
using Audit.Application.Invitations;
using Audit.Application.FeatureFlags;
using Audit.Application.Members;
using Audit.Application.Plans;
using Audit.Application.Roles;
using Audit.Application.Tenants;
using Audit.Application.TermsVersions;
using Audit.Domain;

namespace Audit.Application.AuditLogs;

/// <summary>
/// The decision about the details of every operation of <see cref="AuditOperations"/>: the one type of its details, or none (K3).
/// <c>AuditOperationTests</c> fails for an operation without a decision.
/// </summary>
public static class AuditDetailsTypes
{
    // The operations with details, from AuditOperations, for the discriminator of Audit's Api project, which does not reference Audit's
    // Domain (section 2).
    public const string MemberInvited = AuditOperations.MemberInvited;

    public const string MemberLeft = AuditOperations.MemberLeft;

    public const string MemberRemoved = AuditOperations.MemberRemoved;

    public const string MemberRoleChanged = AuditOperations.MemberRoleChanged;

    public const string FeatureFlagAddedToCode = AuditOperations.FeatureFlagAddedToCode;

    public const string FeatureFlagPercentageSet = AuditOperations.FeatureFlagPercentageSet;

    public const string FeatureFlagRemovedFromCode = AuditOperations.FeatureFlagRemovedFromCode;

    public const string FeatureFlagTenantListed = AuditOperations.FeatureFlagTenantListed;

    public const string FeatureFlagTenantUnlisted = AuditOperations.FeatureFlagTenantUnlisted;

    public const string PlanCreated = AuditOperations.PlanCreated;

    public const string PlanUpdated = AuditOperations.PlanUpdated;

    public const string RoleCreated = AuditOperations.RoleCreated;

    public const string RoleUpdated = AuditOperations.RoleUpdated;

    public const string TenantCreated = AuditOperations.TenantCreated;

    public const string TenantEntitlementOverrideRemoved = AuditOperations.TenantEntitlementOverrideRemoved;

    public const string TenantEntitlementOverrideSet = AuditOperations.TenantEntitlementOverrideSet;

    public const string TenantOwnerInvited = AuditOperations.TenantOwnerInvited;

    public const string TenantOwnerMade = AuditOperations.TenantOwnerMade;

    public const string TenantPlanChanged = AuditOperations.TenantPlanChanged;

    public const string TenantPlanCancelled = AuditOperations.TenantPlanCancelled;

    public const string TenantPaymentFailed = AuditOperations.TenantPaymentFailed;

    public const string TenantMovedToDefaultPlan = AuditOperations.TenantMovedToDefaultPlan;

    public const string TenantPlanCancellationUndone = AuditOperations.TenantPlanCancellationUndone;

    public const string TenantPlanChangeScheduled = AuditOperations.TenantPlanChangeScheduled;

    public const string TenantPlanChangeUndone = AuditOperations.TenantPlanChangeUndone;

    public const string TenantPurgeWarned = AuditOperations.TenantPurgeWarned;

    public const string TenantRenamed = AuditOperations.TenantRenamed;

    public const string TenantSuspended = AuditOperations.TenantSuspended;

    public const string TermsVersionCancelled = AuditOperations.TermsVersionCancelled;

    public const string TermsVersionCreated = AuditOperations.TermsVersionCreated;

    public const string TermsVersionUpdated = AuditOperations.TermsVersionUpdated;

    /// <summary>Every operation, with the type of its details, or null for an operation without details.</summary>
    public static IReadOnlyDictionary<string, Type?> OfOperation { get; } = new Dictionary<string, Type?>(StringComparer.Ordinal)
    {
        [AuditOperations.ActivityLogExported] = null,
        [AuditOperations.FeatureFlagAddedToCode] = typeof(FeatureFlagAddedToCodeDetails),
        [AuditOperations.FeatureFlagEveryoneElseOff] = null,
        [AuditOperations.FeatureFlagEveryoneElseOn] = null,
        [AuditOperations.FeatureFlagPercentageSet] = typeof(FeatureFlagPercentageSetDetails),
        [AuditOperations.FeatureFlagRemovedFromCode] = typeof(FeatureFlagRemovedFromCodeDetails),
        [AuditOperations.FeatureFlagTenantListed] = typeof(FeatureFlagTenantListedDetails),
        [AuditOperations.FeatureFlagTenantUnlisted] = typeof(FeatureFlagTenantUnlistedDetails),
        [AuditOperations.InvitationRevoked] = null,
        [AuditOperations.MemberInvited] = typeof(MemberInvitedDetails),
        [AuditOperations.MemberJoined] = null,
        [AuditOperations.MemberLeft] = typeof(MemberLeftDetails),
        [AuditOperations.MemberRemoved] = typeof(MemberRemovedDetails),
        [AuditOperations.MemberRoleChanged] = typeof(MemberRoleChangedDetails),
        [AuditOperations.MembersExported] = null,
        [AuditOperations.PlanCreated] = typeof(PlanCreatedDetails),
        [AuditOperations.PlanUpdated] = typeof(PlanUpdatedDetails),
        [AuditOperations.RoleCreated] = typeof(RoleCreatedDetails),
        [AuditOperations.RoleDeleted] = null,
        [AuditOperations.RoleUpdated] = typeof(RoleUpdatedDetails),
        [AuditOperations.SystemAdminGranted] = null,
        [AuditOperations.SystemAdminRevoked] = null,
        [AuditOperations.TenantClosed] = null,
        [AuditOperations.TenantContractEnded] = null,
        [AuditOperations.TenantContractStarted] = null,
        [AuditOperations.TenantCreated] = typeof(TenantCreatedDetails),
        [AuditOperations.TenantEntitlementOverrideRemoved] = typeof(TenantEntitlementOverrideRemovedDetails),
        [AuditOperations.TenantEntitlementOverrideSet] = typeof(TenantEntitlementOverrideSetDetails),
        [AuditOperations.TenantOwnerInvited] = typeof(TenantOwnerInvitedDetails),
        [AuditOperations.TenantOwnerMade] = typeof(TenantOwnerMadeDetails),
        [AuditOperations.TenantPlanChanged] = typeof(TenantPlanChangedDetails),
        [AuditOperations.TenantPlanCancelled] = typeof(TenantPlanCancelledDetails),
        [AuditOperations.TenantPaymentFailed] = typeof(TenantPaymentFailedDetails),
        [AuditOperations.TenantMovedToDefaultPlan] = typeof(TenantMovedToDefaultPlanDetails),
        [AuditOperations.TenantPlanCancellationUndone] = typeof(TenantPlanCancellationUndoneDetails),
        [AuditOperations.TenantPlanChangeScheduled] = typeof(TenantPlanChangeScheduledDetails),
        [AuditOperations.TenantPlanChangeUndone] = typeof(TenantPlanChangeUndoneDetails),
        [AuditOperations.TenantPurgeWarned] = typeof(TenantPurgeWarnedDetails),
        [AuditOperations.TenantPurged] = null,
        [AuditOperations.TenantReactivated] = null,
        [AuditOperations.TenantRenamed] = typeof(TenantRenamedDetails),
        [AuditOperations.TenantReopened] = null,
        [AuditOperations.TenantSuspended] = typeof(TenantSuspendedDetails),
        [AuditOperations.TermsVersionCancelled] = typeof(TermsVersionCancelledDetails),
        [AuditOperations.TermsVersionCreated] = typeof(TermsVersionCreatedDetails),
        [AuditOperations.TermsVersionUpdated] = typeof(TermsVersionUpdatedDetails),
        [AuditOperations.UserDataExportDownloaded] = null,
        [AuditOperations.UserDataExportRequested] = null,
        [AuditOperations.UserDeleted] = null,
        [AuditOperations.UserDeletionRequested] = null,
        [AuditOperations.UserEmailChanged] = null,
        [AuditOperations.UserEmailPreferencesChanged] = null,
        [AuditOperations.UserPreferencesChanged] = null,
        [AuditOperations.UserReactivated] = null,
        [AuditOperations.UserRenamed] = null,
        [AuditOperations.UserSuspended] = null,
    };

    /// <summary>
    /// The stored details of an entry as the type of its operation, or null when the entry has none. A stored object that its operation
    /// has no type for is a fault, not an answer.
    /// </summary>
    public static AuditDetails? Read(string operation, string? details) =>
        details is null ? null
        : OfOperation[operation] is { } type ? (AuditDetails)JsonSerializer.Deserialize(details, type, JsonSerializerOptions.Web)!
        : throw new InvalidOperationException($"The operation {operation} keeps no details.");
}
