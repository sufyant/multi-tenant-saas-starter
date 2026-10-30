using ControlPlane.Contracts.Users;
using SharedKernel;

namespace Notifications.Application.Notifications.DeliverNotification;

/// <summary>
/// One notification to one recipient (U3): the fan-out of an event sends one for each recipient, without a tenant, so its handler declares
/// the recipient alone (R4). The id comes from the event, the type and the recipient, so a copy of the event writes no second row (O4). The
/// recipient's address, language and time zone travel with it for the email, as the invitation's do, and so do the names of the people
/// of the values at that moment, which the email shows and the row never keeps (U2).
/// </summary>
/// <param name="TenantId">The tenant the notification is about; none for a personal one.</param>
/// <param name="Values">The values its texts use: a person by user id, any other value as it was at that moment.</param>
/// <param name="Email">The recipient's address; none for a user without one.</param>
/// <param name="TimeZone">The recipient's IANA time zone; none when they have not chosen one, or in a message of the version before.</param>
/// <param name="People">The names of the people of the values, by user id, for the email; none in a message of the version before.</param>
[MessageName("deliver-notification")]
public sealed record DeliverNotificationCommand(
    Guid NotificationId,
    string Type,
    Guid UserId,
    Guid? TenantId,
    IReadOnlyDictionary<string, string> Values,
    DateTimeOffset OccurredAt,
    string? Email,
    Language Language,
    string? TimeZone = null,
    IReadOnlyDictionary<Guid, UserName>? People = null);
