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
/// Reads a language by its tag only. The default converter also takes a number, so <c>7</c> would bind to no language and <c>0</c> to
/// English.
/// </summary>
public sealed class LanguageConverter() : JsonStringEnumConverter<Language>(allowIntegerValues: false);
