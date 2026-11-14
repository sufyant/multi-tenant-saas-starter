using System.Text.Json.Serialization;

namespace ControlPlane.Api.Me.TermsVersions;

/// <summary>
/// What the signed-in person must do about the terms: nothing; give their acceptance of the version, which the gate after sign-in asks;
/// or read the notice of a version that requires no acceptance, which Home shows until the person closes it. The values are words no other
/// code of the screens uses as a literal (F3).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MyTermsResponseAction>))]
public enum MyTermsResponseAction
{
    [JsonStringEnumMemberName("nothing")]
    None,

    [JsonStringEnumMemberName("acceptance")]
    Accept,

    [JsonStringEnumMemberName("notice")]
    Notice,
}
