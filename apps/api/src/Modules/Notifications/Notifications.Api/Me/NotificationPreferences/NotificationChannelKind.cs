using System.Text.Json.Serialization;

namespace Notifications.Api.Me.NotificationPreferences;

/// <summary>A channel of the notification preferences, a column of the screen. Push joins as a third in step 48.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NotificationChannelKind>))]
public enum NotificationChannelKind
{
    /// <summary>The bell of the web apps.</summary>
    [JsonStringEnumMemberName("web")]
    Web,

    /// <summary>The generic notification email.</summary>
    [JsonStringEnumMemberName("email")]
    Email,
}
