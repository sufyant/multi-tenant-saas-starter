namespace Notifications.Api.Me.EmailPreferences;

/// <summary>
/// The new email preferences: the main switch, and a choice for some categories. A category that cannot reach the user is not kept, and a
/// category left out keeps its choice.
/// </summary>
public sealed record ChangeEmailPreferencesRequest(bool Enabled, IReadOnlyList<CategoryEmailPreferenceRequest> Categories);
