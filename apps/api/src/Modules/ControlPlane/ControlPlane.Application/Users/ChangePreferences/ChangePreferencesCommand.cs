using SharedKernel;

namespace ControlPlane.Application.Users.ChangePreferences;

/// <summary>
/// The signed-in user sets their own language and time zone, which follow them to every tenant and device. It runs without a tenant.
/// The time zone is an IANA name, such as <c>Europe/Dublin</c>, or null for "not set".
/// </summary>
public sealed record ChangePreferencesCommand(string ActorId, Language Language, string? TimeZone);
