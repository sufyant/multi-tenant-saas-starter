using SharedKernel;

namespace Notifications.Application.Notifications.GetNotificationPermissions;

/// <summary>The permissions of the reader in the tenant that the envelope declares (W3).</summary>
[MessageName("get-notification-permissions")]
public sealed record GetNotificationPermissionsQuery(Guid UserId);
