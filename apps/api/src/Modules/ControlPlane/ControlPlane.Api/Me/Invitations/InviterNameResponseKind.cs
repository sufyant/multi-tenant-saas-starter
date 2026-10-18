using System.Text.Json.Serialization;

namespace ControlPlane.Api.Me.Invitations;

/// <summary>Who invited: a user of the tenant, or the platform, for which a system admin acted.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InviterNameResponseKind>))]
public enum InviterNameResponseKind
{
    [JsonStringEnumMemberName("user")]
    User,

    [JsonStringEnumMemberName("platform")]
    Platform,
}
