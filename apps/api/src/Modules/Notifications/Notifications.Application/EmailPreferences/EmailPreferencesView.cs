namespace Notifications.Application.EmailPreferences;

/// <summary>A user's email preferences as the screen shows them (U5): the main switch, and each category that can reach the user.</summary>
public sealed record EmailPreferencesView(bool Enabled, IReadOnlyList<CategoryEmailPreferenceView> Categories);
