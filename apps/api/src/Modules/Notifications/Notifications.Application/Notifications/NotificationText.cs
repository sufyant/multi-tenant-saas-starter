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
/// word of a closed list and an entitlement's word are written in the language, a date in the language and the reader's time zone, a number
/// of days with its noun in the form of its plural category in the language, and any other value as stored. A type whose optional value is missing takes its text without that value, or without the value it goes with. A type, a word or a value the texts do
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

    /// <summary>The name of each area of the list, under <c>notificationWords</c>.</summary>
    public const string AreaWords = "area";

    /// <summary>The form of a number of days for each CLDR plural category, under <c>notificationWords</c>; <c>#</c> stands for the number.</summary>
    public const string DaysWords = "days";

    private static readonly Dictionary<Language, Texts> ByLanguage = Read();

    /// <summary>The types the texts of the language have, for the architecture test of the catalog (U1).</summary>
    public static IReadOnlyCollection<string> TypesOf(Language language) => ByLanguage[language].Types.Keys;

    /// <summary>Whether the type has the footer of its email in the language, for the architecture test of the catalog (U5).</summary>
    public static bool HasFooter(Language language, string type) => ByLanguage[language].Types[type].Footer is not null;

    /// <summary>The names of the values that a type's footer uses in the language, for the architecture test of the catalog (U5).</summary>
    public static IReadOnlySet<string> FooterPlaceholdersOf(Language language, string type) =>
        Placeholder().Matches(ByLanguage[language].Types[type].Footer ?? "").Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The footer of a type's email in the language: why the email came (U5), with the values it names written as in the title and the body.
    /// A type of the email without one is a mistake of ours.
    /// </summary>
    public static string FooterOf(
        string type,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<Guid, UserName> people,
        Language language,
        string? timeZone)
    {
        var texts = ByLanguage[language];
        var footer = texts.Types.GetValueOrDefault(type)?.Footer
            ?? throw new InvalidOperationException($"The notification texts in {Tag(language)} have no footer of {type}.");
        return Fill(footer, Written(NotificationCatalog.Of(type), values, people, texts, language, timeZone));
    }

    /// <summary>
    /// The CLDR plural categories of the language's cardinal numbers, whose forms the words of a number of days need, for the architecture
    /// test of the catalog (U1).
    /// </summary>
    public static IReadOnlyCollection<string> PluralCategoriesOf(Language language) => language switch
    {
        Language.Ar => ["zero", "one", "two", "few", "many", "other"],
        _ => ["one", "other"],
    };

    /// <summary>The words of a closed list in the language, for the architecture test of the catalog (U1).</summary>
    public static IReadOnlyCollection<string> WordsOf(Language language, string list) =>
        ByLanguage[language].Words.TryGetValue(list, out var words) ? words.Keys : [];

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
            var name = missing.With ?? missing.Name;
            var without = text.Without?.GetValueOrDefault(name)
                ?? throw new InvalidOperationException($"The notification texts in {Tag(language)} have no text of {type} without {name}.");
            text = new TypeTexts(without.Title ?? text.Title, without.Body, null, null);
        }

        var written = Written(kind, values, people, texts, language, timeZone);
        return new(Fill(text.Title!, written), Fill(text.Body, written));
    }

    /// <summary>The area of a type in the language, as the list shows it beside the item.</summary>
    public static string AreaOf(string type, Language language) =>
        WordOf(ByLanguage[language], language, AreaWords, NotificationCatalog.Of(type).Area);

    /// <summary>
    /// The name of the person who acted, named now as a value's person is; none when the type has no actor or its optional actor is
    /// missing.
    /// </summary>
    public static string? ActorOf(string type, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<Guid, UserName> people, Language language) =>
        NotificationCatalog.Of(type).Actor is { } actor && values.TryGetValue(actor, out var stored)
            ? NameOf(stored, people, ByLanguage[language], language)
            : null;

    // Each stored value of the type, written in the language.
    private static Dictionary<string, string> Written(
        NotificationKind kind,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<Guid, UserName> people,
        Texts texts,
        Language language,
        string? timeZone) =>
        kind.Values
            .Where(value => values.ContainsKey(value.Name))
            .ToDictionary(value => value.Name, value => Write(value, values, people, texts, language, timeZone), StringComparer.Ordinal);

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
            NotificationValueKind.Person => NameOf(stored, people, texts, language),
            NotificationValueKind.Role => EmailLanguage.Of(language).RoleName(stored, bool.Parse(values[$"{value.Name}BuiltIn"])),
            NotificationValueKind.Word => WordOf(texts, language, value.Name, stored),
            NotificationValueKind.EntitlementKey => WordOf(texts, language, EntitlementKeyWords, stored),
            NotificationValueKind.Entitlement => int.TryParse(stored, NumberStyles.None, CultureInfo.InvariantCulture, out _)
                ? stored
                : WordOf(texts, language, EntitlementWords, stored),
            NotificationValueKind.Date => EmailLanguage.Of(language).Dates.Write(DateTimeOffset.Parse(stored, CultureInfo.InvariantCulture), timeZone).Day,
            NotificationValueKind.Days => WordOf(texts, language, DaysWords, PluralCategoryOf(language, int.Parse(stored, NumberStyles.None, CultureInfo.InvariantCulture)))
                .Replace("#", stored, StringComparison.Ordinal),
            _ => stored,
        };
    }

    // The CLDR plural category of a whole number (Unicode CLDR, "Language Plural Rules"). Arabic: zero, one and two by the number, few
    // for 3 to 10 and many for 11 to 99 by its last two digits, other for the rest. English and Turkish: one for 1, other for the rest.
    private static string PluralCategoryOf(Language language, int number) => (language, number, number % 100) switch
    {
        (Language.Ar, 0, _) => "zero",
        (_, 1, _) => "one",
        (Language.Ar, 2, _) => "two",
        (Language.Ar, _, >= 3 and <= 10) => "few",
        (Language.Ar, _, >= 11) => "many",
        _ => "other",
    };

    private static string NameOf(string stored, IReadOnlyDictionary<Guid, UserName> people, Texts texts, Language language) =>
        people.TryGetValue(Guid.Parse(stored), out var person) && !person.Deleted
            ? person.DisplayName ?? WordOf(texts, language, PersonWords, "unnamed")
            : WordOf(texts, language, PersonWords, "deleted");

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

    private sealed record Texts(Dictionary<string, TypeTexts> Types, Dictionary<string, Dictionary<string, string>> Words);

    // A type's texts, its texts without each optional value, which may keep the main title, and the footer of its email.
    private sealed record TypeTexts(string? Title, string Body, Dictionary<string, TypeTexts>? Without, string? Footer);
}
