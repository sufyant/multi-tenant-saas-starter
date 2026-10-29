namespace Notifications.Application.Notifications;

/// <summary>A notification as a bell reads it, before its texts are rendered in the reader's language (U2).</summary>
public sealed record NotificationView(
    Guid Id,
    string Type,
    Guid? TenantId,
    IReadOnlyDictionary<string, string> Values,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
