using System.Text.Json.Serialization;

namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The kind of target of an audit entry.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditTargetResponseKind>))]
public enum AuditTargetResponseKind
{
    [JsonStringEnumMemberName("tenant")]
    Tenant,

    [JsonStringEnumMemberName("user")]
    User,

    [JsonStringEnumMemberName("invitation")]
    Invitation,

    [JsonStringEnumMemberName("role")]
    Role,
}
