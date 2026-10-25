namespace Notifications.Application.Email;

/// <summary>What a value of an email is, which decides how the renderer checks it.</summary>
internal enum EmailValueKind
{
    /// <summary>Text: a name, a role, a moment.</summary>
    Text,

    /// <summary>A link the person opens: an absolute https URL.</summary>
    Link,

    /// <summary>A contact: an absolute https URL or a mailto: address.</summary>
    Contact,
}
