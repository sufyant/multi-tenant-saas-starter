using SharedKernel;

namespace Notifications.Application.Notifications.GetNotificationReaderAccess;

/// <summary>Whether the signed-in user may still read: an open stream asks before each send, and closes for a deleted or suspended user (U6, A9).</summary>
[MessageName("get-notification-reader-access")]
public sealed record GetNotificationReaderAccessQuery(string ActorId);
