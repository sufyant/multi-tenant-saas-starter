using System.Text.Json;
using SharedKernel;

namespace Notifications.Application.Email;

/// <summary>
/// What an email needs of a language beyond its texts, from <c>languages.json</c> and <c>time-zone-cities.json</c> of
/// <c>Email/Generated</c>: the names of the built-in roles and the data of its dates, the zones' cities included. The texts are the
/// generated templates'.
/// </summary>
/// <param name="Roles">Each built-in role's name in the language, by the role's stored name: <c>owner</c>, <c>admin</c>, <c>member</c>.</param>
internal sealed record EmailLanguage(IReadOnlyDictionary<string, string> Roles, EmailDates Dates)
{
    private static readonly Dictionary<Language, EmailLanguage> ByLanguage = Read();

    public static EmailLanguage Of(Language language) => ByLanguage[language];

    /// <summary>
    /// The role as the email names it: a built-in role in the language, a custom role as the Owner typed it. A built-in role the emails
    /// do not know is a mistake of ours.
    /// </summary>
    public string RoleName(string name, bool builtIn)
    {
        if (!builtIn)
        {
            return name;
        }

        return Roles.TryGetValue(name, out var translated)
            ? translated
            : throw new InvalidOperationException($"The emails have no name for the built-in role {name}.");
    }

    // The cities come from time-zone-cities.json, each zone's city by language, so each language's dates take theirs.
    private static Dictionary<Language, EmailLanguage> Read()
    {
        var byTag = JsonSerializer.Deserialize<Dictionary<string, EmailLanguage>>(
            EmailTemplate.Read("languages.json"), JsonSerializerOptions.Web)!;
        var cities = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(EmailTemplate.Read("time-zone-cities.json"))!;
        return Enum.GetValues<Language>().ToDictionary(language => language, language =>
        {
            var tag = JsonEnum<Language>.NameOf(language);
            var found = byTag[tag];
            return found with { Dates = found.Dates with { Cities = cities.ToDictionary(zone => zone.Key, zone => zone.Value[tag], StringComparer.Ordinal) } };
        });
    }
}
