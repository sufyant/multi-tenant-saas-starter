namespace Notifications.Application.Email;

/// <summary>A currency in a language: the text before and after the number, and the digits after the decimal mark (2 for EUR, 0 for JPY).</summary>
internal sealed record EmailCurrency(string Prefix, string Suffix, int Digits);
