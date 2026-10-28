using Audit.Api.Tenant.AuditLogs;

namespace Audit.Api.Statuses;

/// <summary>
/// Maps each of Audit's kinds to the kind enum of a response (N7r). Both audit logs build their entries here. A kind a response cannot
/// carry is a fault, not an answer.
/// </summary>
internal static class ResponseStatus
{
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
        _ => throw Unknown(kind),
    };

    private static ArgumentOutOfRangeException Unknown(string kind) =>
        new(nameof(kind), kind, "The response has no value for this kind of Audit.");
}
