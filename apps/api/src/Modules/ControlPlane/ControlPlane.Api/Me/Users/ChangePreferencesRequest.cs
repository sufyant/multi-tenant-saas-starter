using System.Text.Json.Serialization;
using SharedKernel;

namespace ControlPlane.Api.Me.Users;

/// <summary>
/// The user's language, one of the list, and the IANA name of their time zone, such as <c>Europe/Dublin</c>, or null for "not set":
/// the web then uses the browser's. Both are required, so a client that leaves the time zone out never clears it by mistake.
/// </summary>
public sealed record ChangePreferencesRequest([property: JsonRequired] Language Language, [property: JsonRequired] string? TimeZone);
