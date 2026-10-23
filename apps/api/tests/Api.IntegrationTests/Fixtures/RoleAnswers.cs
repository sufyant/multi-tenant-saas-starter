using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Fixtures;

// A role in a response is one object, { id, name, builtIn } (N7k, K5). A built-in role answers its stored name, one lower-case word, and
// its id, which is the tenant's own (A2). A request gives a role by its id.
internal static class RoleAnswers
{
    // The role an answer holds, by its name and whether it is built in. Each tenant has its own role ids, so a test that compares the
    // kind of role leaves the id out; a test of the id reads it on its own.
    public static string Of(JsonElement answer)
    {
        var role = answer.GetProperty("role");
        return $"{role.GetProperty("name").GetString()} built in: {role.GetProperty("builtIn").GetBoolean()}";
    }

    public static string BuiltIn(string name) => $"{name} built in: True";

    public static string Custom(string name) => $"{name} built in: False";

    // The id of the tenant's role with this name, as the member's client reads it from the tenant's role list (ListRoles), so a test
    // gives a role by its name as before. A role the client cannot read, or a name the tenant has not, is a new id, which no tenant has: the
    // request is then refused by the door or answers role.not_found.
    public static async Task<Guid> IdOfAsync(this HttpClient client, Guid tenantId, string role)
    {
        using var response = await client.GetAsync($"/v1/tenants/{tenantId}/roles?pageSize=100", TestContext.Current.CancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Guid.NewGuid();
        }

        var page = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return page.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("name").GetString() == role)
            .Select(item => item.GetProperty("id").GetGuid())
            .DefaultIfEmpty(Guid.NewGuid())
            .First();
    }
}
