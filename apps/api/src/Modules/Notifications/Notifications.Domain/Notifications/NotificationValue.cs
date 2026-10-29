namespace Notifications.Domain.Notifications;

/// <summary>A value that a type's texts name as <c>{Name}</c>.</summary>
public sealed record NotificationValue(string Name, NotificationValueKind Kind);
