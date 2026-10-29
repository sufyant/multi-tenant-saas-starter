namespace Notifications.Api.Me.EmailPreferences;

/// <summary>One category's email switch.</summary>
/// <param name="Category">The category, from the enum NotificationCategory.</param>
/// <param name="Enabled">Whether a notification of the category goes by email too.</param>
public sealed record CategoryEmailPreferenceResponse(string Category, bool Enabled);
