using SharedKernel;

namespace ControlPlane.Api.Me.Users;

/// <summary>The user's preferences, which follow them to every tenant and device. A null time zone is not set.</summary>
public sealed record PreferencesResponse(Language Language, string? TimeZone);
