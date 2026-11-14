using System.Text.Json.Serialization;

namespace ControlPlane.Api.Me.TermsVersions;

/// <summary>
/// What the signed-in person must do about the terms: nothing; accept the version, which the gate after sign-in asks; or read the notice
/// of a version that requires no acceptance, which Home shows until the person closes it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MyTermsResponseAction>))]
public enum MyTermsResponseAction
{
    [JsonStringEnumMemberName("none")]
    None,

    [JsonStringEnumMemberName("accept")]
    Accept,

    [JsonStringEnumMemberName("notice")]
    Notice,
}
