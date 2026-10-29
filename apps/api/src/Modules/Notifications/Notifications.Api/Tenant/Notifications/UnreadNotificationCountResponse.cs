namespace Notifications.Api.Tenant.Notifications;

/// <summary>How many notifications of a bell the reader has not opened.</summary>
public sealed record UnreadNotificationCountResponse(int Unread);
