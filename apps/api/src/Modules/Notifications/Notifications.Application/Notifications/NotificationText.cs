using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ControlPlane.Contracts.Users;
using Notifications.Application.Email;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Notifications.Application.Notifications;

/// <summary>
/// A notification's title and body in one language (U2): the texts of its type from <c>packages/emails</c>, which <c>pnpm generate</c>
/// writes into <c>Email/Generated/notifications.json</c>, filled with its stored values. The bell and the generic email both render it
/// here, so a text is written once. A person is named from <c>people</c>, which the caller reads now by user id: a deleted user, or one
/// the directory does not know, reads as "Deleted user", and a user without a name as "Unnamed user", in the language. A built-in role, a
/// word of a closed list and an entitlement's word are written in the language, a date in the language and the reader's time zone, and any
/// other value as stored. A type whose optional value is missing takes its text without that value. A type, a word or a value the texts do
/// not have is a mistake of ours, as in an email.
/// </summary>
public sealed partial record NotificationText(string Title, string Body)
{
    /// <summary>The words of a person the read cannot name, under <c>notificationWords</c>.</summary>
    public const string PersonWords = "person";

    /// <summary>The words of an entitlement's value that is no number, under <c>notificationWords</c>.</summary>
    public const string EntitlementWords = "entitlementValue";

    /// <summary>The name of each entitlement key, under <c>notificationWords</c>.</summary>
    public const string EntitlementKeyWords = "entitlementKey";

    private static readonly Dictionary<Language, Texts> ByLanguage = Read();

    /// <summary>The types the texts of the language have, for the architecture test of the catalog (U1).</summary>
    public static IReadOnlyCollection<string> TypesOf(Language language) => ByLanguage[language].Types.Keys;

    /// <summary>The words of a closed list in the language, for the architecture test of the catalog (U1).</summary>
    public static IReadOnlyCollection<string> WordsOf(Language language, string list) =>
        ByLanguage[language].Words.TryGetValue(list, out var words) ? words.Keys : [];

    /// <summary>The name and the description of a category in the language; none when the texts lack it (U5).</summary>
    public static (string Name, string Description)? CategoryOf(Language language, string category) =>
        ByLanguage[language].Categories.TryGetValue(category, out var texts) ? (texts.Name, texts.Description) : null;

    /// <summary>
    /// Why the recipient got a type's email, in the language: a required type always goes; any other went because its category's emails
    /// are on, which the footer names (U4).
    /// </summary>
    public static string FooterOf(string type, Language language)
    {
        var texts = ByLanguage[language];
        var kind = NotificationCatalog.Of(type);
        return kind.Required
            ? texts.Footers.Required
            : Fill(texts.Footers.Category, new Dictionary<string, string> { ["categoryName"] = texts.Categories[kind.Category].Name });
    }

    /// <summary>
    /// The names of the values that a type's texts use in the language: its main texts, or, with <paramref name="without"/>, its texts
    /// without that optional value; none when it has no such texts.
    /// </summary>
    public static IReadOnlySet<string>? PlaceholdersOf(Language language, string type, string? without = null)
    {
        var texts = ByLanguage[language].Types[type];
        var chosen = without is null ? texts : texts.Without?.GetValueOrDefault(without);
        return chosen is null
            ? null
            : Placeholder().Matches((chosen.Title ?? texts.Title) + chosen.Body).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    public static NotificationText Render(
        string type,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<Guid, UserName> people,
        Language language,
        string? timeZone)
    {
        var texts = ByLanguage[language];
        var text = texts.Types.TryGetValue(type, out var found)
            ? found
            : throw new InvalidOperationException($"The notification texts in {Tag(language)} have no type {type}.");
        var kind = NotificationCatalog.Of(type);
        if (kind.Values.FirstOrDefault(value => value.Optional && !values.ContainsKey(value.Name)) is { } missing)
        {
            var without = text.Without?.GetValueOrDefault(missing.Name)
                ?? throw new InvalidOperationException($"The notification texts in {Tag(language)} have no text of {type} without {missing.Name}.");
            text = new TypeTexts(without.Title ?? text.Title, without.Body, null);
        }

        var written = kind.Values
            .Where(value => values.ContainsKey(value.Name))
            .ToDictionary(value => value.Name, value => Write(value, values, people, texts, language, timeZone), StringComparer.Ordinal);
        return new(Fill(text.Title!, written), Fill(text.Body, written));
    }

    private static string Write(
        NotificationValue value,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<Guid, UserName> people,
        Texts texts,
        Language language,
        string? timeZone)
    {
        var stored = values[value.Name];
        return value.Kind switch
        {
            NotificationValueKind.Person => people.TryGetValue(Guid.Parse(stored), out var person) && !person.Deleted
                ? person.DisplayName ?? WordOf(texts, language, PersonWords, "unnamed")
                : WordOf(texts, language, PersonWords, "deleted"),
            NotificationValueKind.Role => EmailLanguage.Of(language).RoleName(stored, bool.Parse(values[$"{value.Name}BuiltIn"])),
            NotificationValueKind.Word => WordOf(texts, language, value.Name, stored),
            NotificationValueKind.EntitlementKey => WordOf(texts, language, EntitlementKeyWords, stored),
            NotificationValueKind.Entitlement => int.TryParse(stored, NumberStyles.None, CultureInfo.InvariantCulture, out _)
                ? stored
                : WordOf(texts, language, EntitlementWords, stored),
            NotificationValueKind.Date => EmailLanguage.Of(language).Dates.Write(DateTimeOffset.Parse(stored, CultureInfo.InvariantCulture), timeZone).Day,
            _ => stored,
        };
    }

    private static string WordOf(Texts texts, Language language, string list, string word) =>
        texts.Words.TryGetValue(list, out var words) && words.TryGetValue(word, out var found)
            ? found
            : throw new InvalidOperationException($"The notification texts in {Tag(language)} have no word {word} of {list}.");

    private static string Fill(string text, Dictionary<string, string> values) =>
        Placeholder().Replace(text, match => values.TryGetValue(match.Groups[1].Value, out var value)
            ? value
            : throw new InvalidOperationException($"A notification text has a placeholder {{{{{match.Groups[1].Value}}}}} without a value."));

    private static string Tag(Language language) => JsonEnum<Language>.NameOf(language);

    private static Dictionary<Language, Texts> Read()
    {
        var byTag = JsonSerializer.Deserialize<Dictionary<string, Texts>>(EmailTemplate.Read("notifications.json"), JsonSerializerOptions.Web)!;
        return Enum.GetValues<Language>().ToDictionary(language => language, language => byTag[Tag(language)]);
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex Placeholder();

    private sealed record Texts(
        Dictionary<string, TypeTexts> Types,
        Dictionary<string, Dictionary<string, string>> Words,
        Dictionary<string, CategoryTexts> Categories,
        FooterTexts Footers);

    private sealed record CategoryTexts(string Name, string Description);

    private sealed record FooterTexts(string Category, string Required);

    // A type's texts, and its texts without each optional value, which may keep the main title.
    private sealed record TypeTexts(string? Title, string Body, Dictionary<string, TypeTexts>? Without);
}
