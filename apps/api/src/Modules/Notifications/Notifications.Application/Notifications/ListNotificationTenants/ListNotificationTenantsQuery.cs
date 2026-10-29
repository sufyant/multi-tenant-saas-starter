using SharedKernel;

namespace Notifications.Application.Notifications.ListNotificationTenants;

/// <summary>The tenants where the reader is a member, whose permissions decide which categories can reach them (U5).</summary>
[MessageName("list-notification-tenants")]
public sealed record ListNotificationTenantsQuery(Guid UserId);
