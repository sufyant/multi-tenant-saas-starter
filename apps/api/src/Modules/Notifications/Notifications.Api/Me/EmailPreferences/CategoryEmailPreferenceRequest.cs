namespace Notifications.Api.Me.EmailPreferences;

/// <summary>One category's new email switch.</summary>
/// <param name="Category">The category, from the enum NotificationCategory.</param>
/// <param name="Enabled">Whether a notification of the category goes by email too.</param>
public sealed record CategoryEmailPreferenceRequest(string Category, bool Enabled);
