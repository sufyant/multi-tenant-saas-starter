namespace Notifications.Api.Me.EmailPreferences;

/// <summary>
/// The signed-in user's email preferences for notifications (U5): the main switch, and the switch of each category that can reach them. In-app
/// notifications cannot be turned off, and a required type is not listed: it always sends its email.
/// </summary>
public sealed record EmailPreferencesResponse(bool Enabled, IReadOnlyList<CategoryEmailPreferenceResponse> Categories);
