using SharedKernel;

namespace Notifications.Contracts.Notifications;

/// <summary>
/// A product module tells people of an event with one of the product's notification types (X7). The module names the type, the event's
/// id and moment, the tenant it tells of (none for a personal type of no tenant), the values its texts use without the tenant's name, the
/// people it tells, and the person who acted, whom it never tells. Notifications reads the tenant's name of that moment and fans out on the
/// type's channels (U4). It keeps the tenant of the envelope it came from (W3).
/// </summary>
[MessageName("send-notification")]
public sealed record SendNotificationCommand(
    string Type,
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid? TenantId,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyCollection<Guid> RecipientIds,
    Guid? ActorId);
