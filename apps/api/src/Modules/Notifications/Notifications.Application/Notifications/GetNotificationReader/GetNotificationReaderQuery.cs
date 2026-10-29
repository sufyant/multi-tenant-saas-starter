using SharedKernel;

namespace Notifications.Application.Notifications.GetNotificationReader;

/// <summary>The signed-in user as a reader of the bells: none for a person who is not a user yet, who has no notification.</summary>
[MessageName("get-notification-reader")]
public sealed record GetNotificationReaderQuery(string ActorId);
