using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.TermsVersions;

/// <summary>Whether a user who may accept accepted the version, or not yet, as the frames name the two tabs.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TermsPersonResponseStatus>))]
public enum TermsPersonResponseStatus
{
    [JsonStringEnumMemberName("accepted")]
    Accepted,

    [JsonStringEnumMemberName("not_yet")]
    NotYet,
}
