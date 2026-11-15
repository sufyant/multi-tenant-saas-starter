using Audit.Api.Tenant.AuditLogs;

namespace Audit.Api.Statuses;

/// <summary>
/// Maps each of Audit's kinds to the kind enum of a response (N7r). Both audit logs build their entries here. A kind a response cannot
/// carry is a fault, not an answer.
/// </summary>
internal static class ResponseStatus
{
    /// <summary>A meter's overage at the quota, as a plan's entry keeps it (step 40).</summary>
    public static AuditMeterSettingResponseOverage OfMeterOverage(string overage) => overage switch
    {
        "Stop" => AuditMeterSettingResponseOverage.Stop,
        "Charge" => AuditMeterSettingResponseOverage.Charge,
        _ => throw Unknown(overage),
    };

    public static AuditActorResponseKind OfActor(string kind) => kind switch
    {
        "User" => AuditActorResponseKind.User,
        "Platform" => AuditActorResponseKind.Platform,
        _ => throw Unknown(kind),
    };

    public static AuditTargetResponseKind OfTarget(string kind) => kind switch
    {
        "Tenant" => AuditTargetResponseKind.Tenant,
        "User" => AuditTargetResponseKind.User,
        "Invitation" => AuditTargetResponseKind.Invitation,
        "Role" => AuditTargetResponseKind.Role,
        "Plan" => AuditTargetResponseKind.Plan,
        "TermsVersion" => AuditTargetResponseKind.TermsVersion,
        "FeatureFlag" => AuditTargetResponseKind.FeatureFlag,
        _ => throw Unknown(kind),
    };

    private static ArgumentOutOfRangeException Unknown(string kind) =>
        new(nameof(kind), kind, "The response has no value for this kind of Audit.");
}
