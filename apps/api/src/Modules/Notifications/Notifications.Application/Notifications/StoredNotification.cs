namespace Notifications.Application.Notifications;

/// <summary>
/// A notification as a bell's read finds it, before its people are named (U7): its type, its stored values, the tenant it is about or
/// none, when it came and when the reader opened it.
/// </summary>
public sealed record StoredNotification(
    Guid Id,
    string Type,
    IReadOnlyDictionary<string, string> Values,
    Guid? TenantId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
