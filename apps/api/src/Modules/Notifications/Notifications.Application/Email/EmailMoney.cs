using System.Globalization;

namespace Notifications.Application.Email;

/// <summary>
/// How a language writes an amount of money in an email, from <c>packages/emails</c>, which reads it from CLDR, as the apps' one formatter,
/// <c>formatMoney</c> of <c>packages/ui</c>, writes it: the image has no ICU (ARCHITECTURE.md, section 7). An amount is a whole number of
/// its currency's minor unit with its ISO 4217 code (Fowler, *Patterns of Enterprise Application Architecture*, "Money"). A whole amount
/// shows no decimals: <c>€100</c>, <c>€12.50</c>, <c>‏100 €</c>. Digits are Western, and the integer part is grouped by three.
/// </summary>
/// <param name="Group">The mark between the groups of three digits.</param>
/// <param name="Decimal">The mark before the decimals.</param>
/// <param name="Currencies">For each currency code, the text before and after the number and the digits of its minor unit.</param>
internal sealed record EmailMoney(string Group, string Decimal, IReadOnlyDictionary<string, EmailCurrency> Currencies)
{
    /// <summary>The amount in minor units of the currency, written in the language. A currency the data does not know is a mistake of ours.</summary>
    public string Write(long minor, string currency)
    {
        var found = Currencies.TryGetValue(currency, out var known)
            ? known
            : throw new InvalidOperationException($"The emails have no currency {currency}.");
        var unit = (long)Math.Pow(10, found.Digits);
        var whole = Grouped((Math.Abs(minor) / unit).ToString(CultureInfo.InvariantCulture));
        var fraction = Math.Abs(minor) % unit;
        var number = fraction == 0 ? whole : $"{whole}{Decimal}{fraction.ToString(CultureInfo.InvariantCulture).PadLeft(found.Digits, '0')}";
        return $"{(minor < 0 ? "-" : "")}{found.Prefix}{number}{found.Suffix}";
    }

    private string Grouped(string digits) =>
        digits.Length <= 3 ? digits : $"{Grouped(digits[..^3])}{Group}{digits[^3..]}";
}
