using SharedKernel;

namespace Notifications.Application.Notifications;

/// <summary>
/// The signed-in user who reads a bell: our id of the user, whose rows the read declares (R4), and the language of the texts (U2). A person
/// who is not a user yet has no id and no notification.
/// </summary>
public sealed record NotificationReader(Guid? UserId, Language Language)
{
    public static NotificationReader Nobody { get; } = new(null, Language.En);
}
