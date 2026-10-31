namespace Notifications.Api.Me.NotificationPreferences;

/// <summary>A type of a category, which the screen lists under its row, read only.</summary>
/// <param name="Type">The type, from the enum NotificationType.</param>
/// <param name="Name">Its short name in the reader's language.</param>
public sealed record NotificationTypeNameResponse(string Type, string Name);
