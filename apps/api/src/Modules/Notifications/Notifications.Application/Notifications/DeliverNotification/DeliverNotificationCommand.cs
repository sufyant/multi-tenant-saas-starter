using SharedKernel;

namespace Notifications.Application.Notifications.DeliverNotification;

/// <summary>
/// One notification to one recipient (U3): the fan-out of an event sends one for each recipient, without a tenant, so its handler declares
/// the recipient alone (R4). The id comes from the event, the type and the recipient, so a copy of the event writes no second row (O4). The
/// recipient's address and language travel with it for the email, as the invitation's do.
/// </summary>
/// <param name="TenantId">The tenant the notification is about; none for a personal one.</param>
/// <param name="Values">The values its texts use, as they were at that moment.</param>
/// <param name="Email">The recipient's address; none for a user without one.</param>
[MessageName("deliver-notification")]
public sealed record DeliverNotificationCommand(
    Guid NotificationId,
    string Type,
    Guid UserId,
    Guid? TenantId,
    IReadOnlyDictionary<string, string> Values,
    DateTimeOffset OccurredAt,
    string? Email,
    Language Language);
