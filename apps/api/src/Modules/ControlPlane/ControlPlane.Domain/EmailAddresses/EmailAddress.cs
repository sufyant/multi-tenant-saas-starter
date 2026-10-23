using System.Globalization;

namespace ControlPlane.Domain.EmailAddresses;

/// <summary>
/// An email address in the one form ControlPlane keeps: without the spaces around it, in lower case by the invariant culture. It takes
/// that form once, where the address enters the system: a request, the identity provider's verified addresses, the configuration. The
/// stored addresses are in this form, so two addresses are the same when they are equal, in .NET and in the database alike, and no
/// comparison ignores case again (the maintainer's decision in step r2a).
/// </summary>
internal sealed record EmailAddress
{
    /// <summary>The longest address a column holds (RFC 5321, section 4.5.3.1: a local part of 64 and a domain of 255, with the @).</summary>
    public const int MaxLength = 320;

    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// The address in its form, or none when the text is not an address. The check is the validators' (FluentValidation's
    /// <c>EmailAddress</c>, <c>NotEmpty</c> and <c>MaximumLength</c>), on the text as it came: one @, neither first nor last, not blank,
    /// and at most <see cref="MaxLength"/> characters. So every text a validator accepts is an address here too.
    /// </summary>
    public static EmailAddress? TryCreate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxLength)
        {
            return null;
        }

        var at = text.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at < text.Length - 1 && at == text.LastIndexOf('@')
            ? new EmailAddress(text.Trim().ToLower(CultureInfo.InvariantCulture))
            : null;
    }

    /// <summary>The address in its form, for a text a validator or the configuration's check has already accepted.</summary>
    public static EmailAddress Create(string text) =>
        TryCreate(text) ?? throw new ArgumentException("The text is not an email address.", nameof(text));

    /// <summary>The addresses among these texts, each in its form; a text that is not an address is left out.</summary>
    public static IReadOnlyList<EmailAddress> AllOf(IEnumerable<string> texts) =>
        [.. texts.Select(TryCreate).OfType<EmailAddress>()];

    public override string ToString() => Value;
}
