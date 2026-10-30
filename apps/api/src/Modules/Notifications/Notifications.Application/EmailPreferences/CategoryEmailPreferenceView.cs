namespace Notifications.Application.EmailPreferences;

/// <summary>One category's email switch.</summary>
public sealed record CategoryEmailPreferenceView(string Category, bool Enabled);
