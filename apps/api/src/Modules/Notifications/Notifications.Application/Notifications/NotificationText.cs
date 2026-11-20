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
/// of days with its noun in the form of its plural category in the language, a meter by its word or its key, a count grouped by three, and
/// any other value as stored. A type whose optional value is missing takes its text without that value, or without the value it goes with. A type, a word or a value the texts do
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

    /// <summary>The name of each meter of the code, under <c>notificationWords</c>.</summary>
    public const string MeterWords = "meter";

    /// <summary>The name of each area of the list, under <c>notificationWords</c>.</summary>
    public const string AreaWords = "area";

    /// <summary>The form of a number of days for each CLDR plural category, under <c>notificationWords</c>; <c>#</c> stands for the number.</summary>
    public const string DaysWords = "days";

    /// <summary>The form of a number of feature flags for each CLDR plural category, under <c>notificationWords</c>; <c>#</c> stands for the number.</summary>
    public const string FeatureFlagWords = "featureFlags";

    /// <summary>The form of a number of payment attempts for each CLDR plural category, under <c>notificationWords</c>; <c>#</c> stands for the number.</summary>
    public const string AttemptWords = "attempts";

    /// <summary>The form of a number of people for each CLDR plural category, under <c>notificationWords</c>; <c>#</c> stands for the number.</summary>
    public const string PeopleWords = "people";

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
            : Placeholder().Matches((chosen.Title ?? texts.Title) + chosen.Body + chosen.Email?.Body).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>An amount as a value of type <see cref="NotificationValueKind.Money"/> stores it: the minor units and the currency code.</summary>
    public static string MoneyValue(long minor, string currency) => $"{minor.ToString(CultureInfo.InvariantCulture)} {currency}";

    /// <summary>The notification as the bell shows it: its title and the body of the bell.</summary>
    public static NotificationText Render(
        string type,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<Guid, UserName> people,
        Language language,
        string? timeZone)
    {
        var (texts, text) = Chosen(type, values, language);
        var written = Written(NotificationCatalog.Of(type), values, people, texts, language, timeZone);
        return new(Fill(text.Title!, written), Fill(text.Body, written));
    }

    /// <summary>
    /// The notification as its email writes it: its title, the body of its email, which says more than the bell's where the type has one,
    /// and the label of its one button, the type's own or the generic "Open {brandName}".
    /// </summary>
    public static NotificationEmailText RenderEmail(
        string type,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<Guid, UserName> people,
        Language language,
        string? timeZone)
    {
        var (texts, text) = Chosen(type, values, language);
        var written = Written(NotificationCatalog.Of(type), values, people, texts, language, timeZone);
        var button = texts.Types[type].Email?.Button ?? texts.Button;
        return new(Fill(text.Title!, written), Fill(text.Email?.Body ?? text.Body, written), button);
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

    // The texts of the language and the type's texts for its values: its main texts, or its texts without the first optional value that is
    // missing, which keep the main title unless they have their own.
    private static (Texts Texts, TypeTexts Text) Chosen(string type, IReadOnlyDictionary<string, string> values, Language language)
    {
        var texts = ByLanguage[language];
        var text = texts.Types.TryGetValue(type, out var found)
            ? found
            : throw new InvalidOperationException($"The notification texts in {Tag(language)} have no type {type}.");
        if (NotificationCatalog.Of(type).Values.FirstOrDefault(value => value.Optional && !values.ContainsKey(value.Name)) is { } missing)
        {
            var name = missing.With ?? missing.Name;
            var without = text.Without?.GetValueOrDefault(name)
                ?? throw new InvalidOperationException($"The notification texts in {Tag(language)} have no text of {type} without {name}.");
            text = new TypeTexts(without.Title ?? text.Title, without.Body, without.Email, null, null);
        }

        return (texts, text);
    }

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
            NotificationValueKind.Day => EmailLanguage.Of(language).Dates.Write(
                new DateTimeOffset(DateOnly.ParseExact(stored, "yyyy-MM-dd", CultureInfo.InvariantCulture), TimeOnly.MinValue, TimeSpan.Zero), null).Day,
            NotificationValueKind.Days => WordOf(texts, language, DaysWords, PluralCategoryOf(language, int.Parse(stored, NumberStyles.None, CultureInfo.InvariantCulture)))
                .Replace("#", stored, StringComparison.Ordinal),
            NotificationValueKind.FeatureFlags => Counted(texts, language, FeatureFlagWords, stored),
            NotificationValueKind.Attempts => Counted(texts, language, AttemptWords, stored),
            NotificationValueKind.People => Counted(texts, language, PeopleWords, stored),
            NotificationValueKind.Money => MoneyOf(stored, language),
            NotificationValueKind.Meter => texts.Words.GetValueOrDefault(MeterWords)?.GetValueOrDefault(stored) ?? stored,
            NotificationValueKind.Count => EmailLanguage.Of(language).Money.Number(long.Parse(stored, NumberStyles.None, CultureInfo.InvariantCulture)),
            _ => stored,
        };
    }

    // A number with its noun in the form of its plural category in the language.
    private static string Counted(Texts texts, Language language, string list, string stored) =>
        WordOf(texts, language, list, PluralCategoryOf(language, int.Parse(stored, NumberStyles.None, CultureInfo.InvariantCulture)))
            .Replace("#", stored, StringComparison.Ordinal);

    // An amount as stored, "10000 EUR": the minor units and the currency code.
    private static string MoneyOf(string stored, Language language)
    {
        var (minor, currency) = stored.Split(' ') is [var amount, var code]
            ? (long.Parse(amount, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), code)
            : throw new InvalidOperationException($"A notification's amount {stored} is not minor units and a currency code.");
        return EmailLanguage.Of(language).Money.Write(minor, currency);
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

    // The texts of a language: each type's, the words of each closed list, and the generic button of a notification's email.
    private sealed record Texts(Dictionary<string, TypeTexts> Types, Dictionary<string, Dictionary<string, string>> Words, string Button);

    // A type's texts, the body and the button of its email where they differ from the bell's, its texts without each optional value, which
    // may keep the main title, and the footer of its email.
    private sealed record TypeTexts(string? Title, string Body, EmailTexts? Email, Dictionary<string, TypeTexts>? Without, string? Footer);

    private sealed record EmailTexts(string Body, string? Button);
}
