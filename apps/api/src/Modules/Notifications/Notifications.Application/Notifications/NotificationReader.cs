using SharedKernel;

namespace Notifications.Application.Notifications;

/// <summary>
/// The signed-in user who reads a bell: our id of the user, whose rows the read declares (R4), and the language and the IANA time zone of
/// the texts (U2), none when the user has not chosen one. A person
/// who is not a user yet has no id and no notification. Whether the reader is a system admin decides whether the platform's category can
/// reach them (U5).
/// </summary>
public sealed record NotificationReader(Guid? UserId, Language Language, bool IsSystemAdmin, string? TimeZone = null)
{
    public static NotificationReader Nobody { get; } = new(null, Language.En, IsSystemAdmin: false);
}
