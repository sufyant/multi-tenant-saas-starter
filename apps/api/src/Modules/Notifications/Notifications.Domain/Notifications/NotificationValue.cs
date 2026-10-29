namespace Notifications.Domain.Notifications;

/// <summary>
/// A value that a type's texts name as <c>{Name}</c>. A word of a closed list names its words, which the texts translate into every
/// language (U2).
/// </summary>
public sealed record NotificationValue(string Name, NotificationValueKind Kind, IReadOnlyList<string>? Words = null);
