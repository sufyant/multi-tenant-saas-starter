namespace Notifications.Api.Tenant.Notifications;

/// <summary>Where a notification leads: a kind, and the tenant it is about, none for a personal notification.</summary>
public sealed record NotificationLinkResponse(NotificationLinkResponseKind Kind, Guid? TenantId);
