namespace Notifications.Domain.Notifications;

/// <summary>
/// A value that a type's texts name as <c>{Name}</c>. A word of a closed list names its words, which the texts translate into every
/// language (U2). An optional value may be missing: the texts then have a text of their own without it, such as a role change by the
/// platform, which names no person.
/// </summary>
public sealed record NotificationValue(string Name, NotificationValueKind Kind, IReadOnlyList<string>? Words = null, bool Optional = false);
