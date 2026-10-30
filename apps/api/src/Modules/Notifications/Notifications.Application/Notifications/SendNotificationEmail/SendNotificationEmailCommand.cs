using ControlPlane.Contracts.Users;
using SharedKernel;

namespace Notifications.Application.Notifications.SendNotificationEmail;

/// <summary>
/// The generic notification email to one recipient, which the dispatcher decided on (U4), with the names of the people of its values at the
/// moment of the event and the recipient's time zone.
/// </summary>
[MessageName("send-notification-email")]
public sealed record SendNotificationEmailCommand(
    Guid NotificationId,
    string Type,
    IReadOnlyDictionary<string, string> Values,
    string Email,
    Language Language,
    string? TimeZone = null,
    IReadOnlyDictionary<Guid, UserName>? People = null);
