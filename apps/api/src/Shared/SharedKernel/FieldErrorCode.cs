using System.Text.Json.Serialization;

namespace SharedKernel;

/// <summary>
/// Why one field of a request failed validation. A validation answer (<see cref="ErrorCode.RequestValidationFailed"/>) lists these
/// for each field, never a message, so a screen writes its own text. A validator rule states its code by the member's name
/// (<c>WithErrorCode(nameof(FieldErrorCode.Required))</c>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FieldErrorCode>))]
public enum FieldErrorCode
{
    [JsonStringEnumMemberName("required")]
    Required,

    [JsonStringEnumMemberName("email_invalid")]
    EmailInvalid,

    [JsonStringEnumMemberName("too_long")]
    TooLong,
}
