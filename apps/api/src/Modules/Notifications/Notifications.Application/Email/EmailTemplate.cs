using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using SharedKernel;

namespace Notifications.Application.Email;

/// <summary>
/// One email of <c>packages/emails</c> in one language, as <c>pnpm generate</c> renders it into <c>Email/Generated</c>: a subject, an
/// HTML part and a plain-text part, each with a placeholder <c>{{name}}</c> for each value. The assembly embeds the files, so the image
/// needs no Node and no file of its own. This is the one renderer of every email.
/// </summary>
internal sealed partial record EmailTemplate(string Subject, string Html, string Text)
{
    // The logical name of each embedded file of Email/Generated (the project file).
    private const string ResourcePrefix = "Notifications.Email.";

    /// <summary>The product's name and support contact, from <c>packages/brand</c>.</summary>
    public static EmailBrand Brand { get; } = ReadBrand();

    /// <summary>The email of this name, such as <c>invitation</c>, in the language.</summary>
    public static EmailTemplate Of(string email, Language language)
    {
        var tag = JsonEnum<Language>.NameOf(language);
        return new(Read($"{email}.{tag}.subject.txt"), Read($"{email}.{tag}.html"), Read($"{email}.{tag}.txt"));
    }

    /// <summary>
    /// The email to the address, its placeholders filled. Each value is HTML-encoded in the HTML part and stays as written in the subject and
    /// the text part. A link must be an absolute https URL, and a contact an absolute https URL or a mailto: address. Each placeholder needs
    /// a value, and each value a placeholder. Anything else is a mistake of ours, so the email is not sent.
    /// </summary>
    public EmailMessage Write(string to, string idempotencyKey, IReadOnlyList<EmailValue> values)
    {
        foreach (var value in values)
        {
            Check(value);
        }

        var byName = values.ToDictionary(value => value.Name);
        var placeholders = new[] { Subject, Html, Text }
            .SelectMany(part => Placeholder().Matches(part))
            .Select(match => match.Groups[1].Value)
            .ToHashSet();
        if (placeholders.FirstOrDefault(name => !byName.ContainsKey(name)) is { } missing)
        {
            throw new InvalidOperationException($"The email has a placeholder {{{{{missing}}}}} without a value.");
        }

        if (byName.Keys.FirstOrDefault(name => !placeholders.Contains(name)) is { } extra)
        {
            throw new InvalidOperationException($"The email has no placeholder for the value {extra}.");
        }

        return new EmailMessage(
            to,
            Fill(Subject, byName, value => value),
            Fill(Html, byName, HtmlEncoded),
            Fill(Text, byName, value => value),
            idempotencyKey);
    }

    /// <summary>The text of an embedded file of <c>Email/Generated</c>.</summary>
    internal static string Read(string file)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + file)
            ?? throw new InvalidOperationException($"The generated email file {file} is missing. Run pnpm generate.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static EmailBrand ReadBrand()
    {
        using var brand = JsonDocument.Parse(Read("brand.json"));
        return new(brand.RootElement.GetProperty("name").GetString()!, new Uri(brand.RootElement.GetProperty("supportUrl").GetString()!));
    }

    // The message names the value, never its content: a link carries an invitation's secret.
    private static void Check(EmailValue value)
    {
        if (value.Kind == EmailValueKind.Text)
        {
            return;
        }

        var scheme = Uri.TryCreate(value.Value, UriKind.Absolute, out var uri) ? uri.Scheme : null;
        if (scheme != Uri.UriSchemeHttps && !(value.Kind == EmailValueKind.Contact && scheme == Uri.UriSchemeMailto))
        {
            throw new InvalidOperationException(
                $"The value {value.Name} of an email must be an absolute https URL, or a mailto: address for a contact.");
        }
    }

    private static string Fill(string part, Dictionary<string, EmailValue> values, Func<string, string> encode) =>
        Placeholder().Replace(part, match => encode(values[match.Groups[1].Value].Value));

    // The five characters that end a text or a quoted attribute in HTML (OWASP, "Cross Site Scripting Prevention Cheat Sheet", rule 1).
    // Every other character stays as it is: the part is UTF-8.
    private static string HtmlEncoded(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex Placeholder();
}
