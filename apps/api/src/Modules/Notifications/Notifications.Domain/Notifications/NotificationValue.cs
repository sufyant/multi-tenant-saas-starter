namespace Notifications.Domain.Notifications;

/// <summary>
/// A value that a type's texts name as <c>{Name}</c>. A word of a closed list names its words, which the texts translate into every
/// language (U2). An optional value may be missing: the texts then have a text of their own without it, such as a role change by the
/// platform, which names no person. An optional value that is there exactly when another one is names it in <paramref name="With"/>: the
/// two share the text without the other one, such as a period and the date its older entries go.
/// </summary>
public sealed record NotificationValue(string Name, NotificationValueKind Kind, IReadOnlyList<string>? Words = null, bool Optional = false, string? With = null);
