namespace Notifications.Application.Users;

/// <summary>
/// An address as the notice to the old address writes it: the first character of the local part, three stars, and the domain, so the old
/// address learns where the account went without the whole new address (A7).
/// </summary>
public static class MaskedEmail
{
    public static string Of(string email)
    {
        var at = email.LastIndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
