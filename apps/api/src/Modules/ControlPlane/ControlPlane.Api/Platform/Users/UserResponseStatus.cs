using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.Users;

/// <summary>The status of a catalog user. A system admin deleted a deleted user, and the catalog keeps no email address or name of theirs.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UserResponseStatus>))]
public enum UserResponseStatus
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("suspended")]
    Suspended,

    [JsonStringEnumMemberName("deleted")]
    Deleted,
}
