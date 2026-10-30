namespace Notifications.Domain.Notifications;

/// <summary>How a value of a notification's text is written in the reader's language (U2).</summary>
public enum NotificationValueKind
{
    /// <summary>Text as stored: a name as it was at that moment.</summary>
    Text,

    /// <summary>A role: a built-in role in the reader's language, a custom role as the Owner typed it. The value <c>{name}BuiltIn</c> says which.</summary>
    Role,

    /// <summary>A word of a closed list, such as a tenant's new status, which the texts translate.</summary>
    Word,
}
