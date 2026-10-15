using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Hosting;

// K5: sort and each filter of a list are enums in the OpenAPI document, so a generated client sends their values. Minimal API binds an
// enum by its C# name and ignores the JSON name the document shows; each value the document lists must bind.
public sealed class ListParameterBindingTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task CallList_EachValueTheDocumentListsForAnEnumParameter_IsAccepted()
    {
        var document = await _api.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json", Cancellation);

        var calls = await StatusOfEachDocumentedValueAsync(document);

        calls.ShouldNotBeEmpty();
        calls.Where(call => call.Status != HttpStatusCode.OK).Select(call => $"{call.Request}: {call.Status}").ShouldBeEmpty();
    }

    private async Task<List<(string Request, HttpStatusCode Status)>> StatusOfEachDocumentedValueAsync(JsonElement document)
    {
        var calls = new List<(string, HttpStatusCode)>();
        foreach (var (path, operation) in Lists(document))
        {
            var (client, url) = await CallerOfAsync(path);
            foreach (var (name, values) in EnumParametersOf(document, operation))
            {
                foreach (var value in values)
                {
                    var request = $"{url}?{name}={Uri.EscapeDataString(value)}";
                    calls.Add((request, (await client.GetAsync(request, Cancellation)).StatusCode));
                }
            }
        }

        return calls;
    }

    // A caller who may read the list: a system admin, a user, or a member of a new tenant.
    private async Task<(HttpClient Client, string Url)> CallerOfAsync(string path)
    {
        if (path.StartsWith("/v1/admin/", StringComparison.Ordinal))
        {
            return (_api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true), path);
        }

        if (path.StartsWith("/v1/me/", StringComparison.Ordinal))
        {
            return (_api.CreateClient(await _catalog.AddUserAsync()), path);
        }

        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        return (_api.CreateClient(member), path.Replace("{tenantId}", tenant.Id.ToString(), StringComparison.Ordinal));
    }

    private static IEnumerable<(string Path, JsonElement Operation)> Lists(JsonElement document) =>
        document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (path.Name, operation.Value)))
            .Where(list => list.Value.GetProperty("operationId").GetString()!.StartsWith("List", StringComparison.Ordinal));

    private static IEnumerable<(string Name, List<string> Values)> EnumParametersOf(JsonElement document, JsonElement operation) =>
        operation.GetProperty("parameters").EnumerateArray()
            .Where(parameter => parameter.GetProperty("in").GetString() == "query")
            .Select(parameter => (parameter.GetProperty("name").GetString()!, Resolve(document, parameter.GetProperty("schema"))))
            .Where(parameter => parameter.Item2.TryGetProperty("enum", out _))
            .Select(parameter => (parameter.Item1, parameter.Item2.GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToList()));

    private static JsonElement Resolve(JsonElement document, JsonElement schema) =>
        schema.TryGetProperty("$ref", out var reference)
            ? document.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1])
            : schema;
}
