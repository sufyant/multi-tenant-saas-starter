using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharedKernel;

/// <summary>
/// The languages the web speaks: a user's language, and the language of an invitation's email. This is the one list: the OpenAPI
/// document and the messages to Notifications take it from here. A new language is a new member here, with its email texts. Each value
/// is its BCP 47 language tag in JSON and in the database (W3C, "Language tags in HTML and XML").
/// </summary>
[JsonConverter(typeof(LanguageConverter))]
public enum Language
{
    [JsonStringEnumMemberName("en")]
    En,

    [JsonStringEnumMemberName("tr")]
    Tr,

    [JsonStringEnumMemberName("ar")]
    Ar,
}

/// <summary>
/// Reads and writes a language as its exact tag, the name its member declares. The default enum converter also takes a number, so
/// <c>7</c> would bind to no language and <c>0</c> to English, and a list of names with commas, so <c>"tr, ar"</c> would bind to no
/// language and <c>"en, tr"</c> to Turkish.
/// </summary>
public sealed class LanguageConverter : JsonConverter<Language>
{
    private static readonly Dictionary<Language, string> Tags = Enum.GetValues<Language>().ToDictionary(
        language => language,
        language => typeof(Language).GetField(language.ToString())!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name);

    public override Language Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var tag = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return Tags.SingleOrDefault(pair => pair.Value == tag) is { Value: not null } found
            ? found.Key
            : throw new JsonException("A language is one tag of the list.");
    }

    public override void Write(Utf8JsonWriter writer, Language value, JsonSerializerOptions options) => writer.WriteStringValue(Tags[value]);
}
