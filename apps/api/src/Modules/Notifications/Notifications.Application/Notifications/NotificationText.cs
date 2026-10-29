using System.Text.Json;
using System.Text.RegularExpressions;
using Notifications.Application.Email;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Notifications.Application.Notifications;

/// <summary>
/// A notification's title and body in one language (U2): the texts of its type from <c>packages/emails</c>, which <c>pnpm generate</c>
/// writes into <c>Email/Generated/notifications.json</c>, filled with its stored values. The bell and the generic email both render it
/// here, so a text is written once. A built-in role and a word of a closed list are written in the language; any other value as stored.
/// A type, a word or a value the texts do not have is a mistake of ours, as in an email.
/// </summary>
public sealed partial record NotificationText(string Title, string Body)
{
    private static readonly Dictionary<Language, Texts> ByLanguage = Read();

    /// <summary>The types the texts of the language have, for the architecture test of the catalog (U1).</summary>
    public static IReadOnlyCollection<string> TypesOf(Language language) => ByLanguage[language].Types.Keys;

    /// <summary>The words of a value of a closed list in the language, for the architecture test of the catalog (U1).</summary>
    public static IReadOnlyCollection<string> WordsOf(Language language, string value) =>
        ByLanguage[language].Words.TryGetValue(value, out var words) ? words.Keys : [];

    /// <summary>The names of the values that a type's texts use in the language.</summary>
    public static IReadOnlySet<string> PlaceholdersOf(Language language, string type) =>
        Placeholder().Matches(ByLanguage[language].Types[type].Title + ByLanguage[language].Types[type].Body)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    public static NotificationText Render(string type, IReadOnlyDictionary<string, string> values, Language language)
    {
        var texts = ByLanguage[language];
        var text = texts.Types.TryGetValue(type, out var found)
            ? found
            : throw new InvalidOperationException($"The notification texts in {JsonEnum<Language>.NameOf(language)} have no type {type}.");
        var written = NotificationCatalog.Of(type).Values.ToDictionary(value => value.Name, value => Write(value, values, texts, language), StringComparer.Ordinal);
        return new(Fill(text.Title, written), Fill(text.Body, written));
    }

    private static string Write(NotificationValue value, IReadOnlyDictionary<string, string> values, Texts texts, Language language)
    {
        var stored = values.TryGetValue(value.Name, out var found)
            ? found
            : throw new InvalidOperationException($"A notification has no value {value.Name}.");
        return value.Kind switch
        {
            NotificationValueKind.Role => EmailLanguage.Of(language).RoleName(stored, bool.Parse(values[$"{value.Name}BuiltIn"])),
            NotificationValueKind.Word => texts.Words.TryGetValue(value.Name, out var words) && words.TryGetValue(stored, out var word)
                ? word
                : throw new InvalidOperationException($"The notification texts in {JsonEnum<Language>.NameOf(language)} have no word {stored} of {value.Name}."),
            _ => stored,
        };
    }

    private static string Fill(string text, Dictionary<string, string> values) =>
        Placeholder().Replace(text, match => values.TryGetValue(match.Groups[1].Value, out var value)
            ? value
            : throw new InvalidOperationException($"A notification text has a placeholder {{{{{match.Groups[1].Value}}}}} without a value."));

    private static Dictionary<Language, Texts> Read()
    {
        var byTag = JsonSerializer.Deserialize<Dictionary<string, Texts>>(EmailTemplate.Read("notifications.json"), JsonSerializerOptions.Web)!;
        return Enum.GetValues<Language>().ToDictionary(language => language, language => byTag[JsonEnum<Language>.NameOf(language)]);
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex Placeholder();

    private sealed record Texts(Dictionary<string, TypeTexts> Types, Dictionary<string, Dictionary<string, string>> Words);

    private sealed record TypeTexts(string Title, string Body);
}
