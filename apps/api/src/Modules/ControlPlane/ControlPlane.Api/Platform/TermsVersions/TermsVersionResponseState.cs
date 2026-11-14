using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.TermsVersions;

/// <summary>
/// Where a terms version stands on the day of the request, which the days decide: scheduled until its day comes, then in force until a
/// later version takes effect, then replaced; or cancelled before its day. The values are words no other code of the screens uses as a
/// literal (F3).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TermsVersionResponseState>))]
public enum TermsVersionResponseState
{
    [JsonStringEnumMemberName("scheduled")]
    Scheduled,

    [JsonStringEnumMemberName("in_force")]
    Current,

    [JsonStringEnumMemberName("replaced")]
    Replaced,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}
