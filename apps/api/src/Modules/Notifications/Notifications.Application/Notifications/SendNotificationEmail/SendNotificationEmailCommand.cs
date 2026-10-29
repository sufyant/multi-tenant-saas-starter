using SharedKernel;

namespace Notifications.Application.Notifications.SendNotificationEmail;

/// <summary>The generic notification email to one recipient, which the dispatcher decided on (U4).</summary>
[MessageName("send-notification-email")]
public sealed record SendNotificationEmailCommand(
    Guid NotificationId,
    string Type,
    IReadOnlyDictionary<string, string> Values,
    string Email,
    Language Language);
