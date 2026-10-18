using SharedKernel;

namespace ControlPlane.Application.Users.ChangePreferences;

/// <summary>The user's preferences, as the user keeps them.</summary>
public sealed record PreferencesView(Language Language, string? TimeZone);
