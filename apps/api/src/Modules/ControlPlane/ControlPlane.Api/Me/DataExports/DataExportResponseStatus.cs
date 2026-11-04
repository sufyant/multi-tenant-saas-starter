using System.Text.Json.Serialization;

namespace ControlPlane.Api.Me.DataExports;

/// <summary>
/// Where a data export is: being prepared, ready to download until its expiry, failed, so the person can ask again, or expired, its file
/// deleted.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DataExportResponseStatus>))]
public enum DataExportResponseStatus
{
    [JsonStringEnumMemberName("preparing")]
    Preparing,

    [JsonStringEnumMemberName("ready")]
    Ready,

    [JsonStringEnumMemberName("failed")]
    Failed,

    [JsonStringEnumMemberName("expired")]
    Expired,
}
