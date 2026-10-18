using System.Text.Json.Serialization;

namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The kind of actor of an audit entry.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditActorResponseKind>))]
public enum AuditActorResponseKind
{
    [JsonStringEnumMemberName("user")]
    User,

    [JsonStringEnumMemberName("platform")]
    Platform,
}
