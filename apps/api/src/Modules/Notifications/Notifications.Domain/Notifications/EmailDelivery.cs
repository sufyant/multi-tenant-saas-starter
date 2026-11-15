namespace Notifications.Domain.Notifications;

/// <summary>
/// A delivery of a type that goes by email only, which writes no notification (U4): the dispatcher keeps it under the notification's id, so
/// a copy of the event finds it and sends no second email, however late the copy comes (O4). It holds no value and no text.
/// </summary>
internal sealed class EmailDelivery(Guid id, Guid userId, DateTimeOffset deliveredAt)
{
    public Guid Id { get; private init; } = id;

    public Guid UserId { get; private init; } = userId;

    public DateTimeOffset DeliveredAt { get; private init; } = deliveredAt;
}
