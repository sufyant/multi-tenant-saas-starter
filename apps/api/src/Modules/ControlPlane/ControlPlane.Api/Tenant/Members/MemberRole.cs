using System.Text.Json.Serialization;
using SharedKernel;

namespace ControlPlane.Api.Tenant.Members;

/// <summary>
/// A built-in role: the built-in role of every role in a response, the role filter of the member and the invitation lists, the role an
/// invitation gives, and the role a member is given.
/// </summary>
[JsonConverter(typeof(ExactNameEnumConverter<MemberRole>))]
public enum MemberRole
{
    [JsonStringEnumMemberName("owner")]
    Owner,

    [JsonStringEnumMemberName("admin")]
    Admin,

    [JsonStringEnumMemberName("member")]
    Member,
}
