using System.Text.Json.Serialization;

namespace ControlPlane.Api.Tenant.Invitations;

/// <summary>
/// The state of an invitation at the moment of the request: pending, expired (pending past its expiry), accepted (used) or cancelled
/// (revoked, replaced, or cancelled with its tenant). Every response that holds the state of an invitation uses this one enum, and says
/// which of its values it can hold.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InvitationResponseState>))]
public enum InvitationResponseState
{
    [JsonStringEnumMemberName("pending")]
    Pending,

    [JsonStringEnumMemberName("expired")]
    Expired,

    [JsonStringEnumMemberName("accepted")]
    Accepted,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}
