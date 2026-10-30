namespace Notifications.Domain.Notifications;

/// <summary>How a value of a notification's text is stored, and how the read writes it in the reader's language (U2).</summary>
public enum NotificationValueKind
{
    /// <summary>Text as stored: a name that is not a person's, as it was at that moment, such as a tenant's or a plan's.</summary>
    Text,

    /// <summary>A person, stored by user id only (K3). The read names them now; a deleted user reads as "Deleted user" in the language.</summary>
    Person,

    /// <summary>A role: a built-in role in the reader's language, a custom role as the Owner typed it. The value <c>{name}BuiltIn</c> says which.</summary>
    Role,

    /// <summary>A word of a closed list, which the texts translate.</summary>
    Word,

    /// <summary>A key of ControlPlane's entitlement list, written by its name in the language, as the plan screens write it.</summary>
    EntitlementKey,

    /// <summary>An entitlement's value: a number as stored; <c>unlimited</c>, <c>on</c>, <c>off</c> and <c>not_set</c> in the language.</summary>
    Entitlement,

    /// <summary>A moment, stored in ISO 8601, written as a date in the reader's language and time zone.</summary>
    Date,
}
