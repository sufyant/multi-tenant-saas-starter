using System.Text.Json;

namespace Api.IntegrationTests.Fixtures;

// A role in a response is one object, { builtIn, name } (N7, K5). A built-in role answers its one word in both fields.
internal static class Roles
{
    // The role an answer holds, as the JSON text the API wrote.
    public static string Of(JsonElement answer) => answer.GetProperty("role").GetRawText();

    public static string BuiltIn(string name) => $$"""{"builtIn":"{{name}}","name":"{{name}}"}""";
}
