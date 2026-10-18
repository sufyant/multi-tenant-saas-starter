using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Hosting;

// K5: sort and each filter of a list are enums in the OpenAPI document, so a generated client sends their values. Minimal API binds an
// enum by its C# name and ignores the JSON name the document shows; each value the document lists must bind. One case for each list
// of the page contract; a list with a cursor (the audit logs) has no sort and no filter.
public sealed class ListParameterBindingTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private JsonElement _document;

    public static TheoryData<string, string> Lists => new()
    {
        { "ListTenants", "platform" },
        { "ListUsers", "platform" },
        { "ListMyTenants", "user" },
        { "ListMembers", "member" },
        { "ListInvitations", "member" },
    };

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        _document = await _api.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json", Cancellation);
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public void DescribeLists_EveryListOfTheDocument_IsACaseOfTheBindingTest()
    {
        var documented = ListsOf(_document).Select(list => list.Id);

        documented.ShouldBe(Lists.Select(list => list.Data.Item1), ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public async Task CallList_EachValueTheDocumentListsForAnEnumParameter_IsAccepted(string operationId, string caller)
    {
        var (path, operation) = ListsOf(_document).Single(list => list.Id == operationId).Value;
        var (client, url) = await CallerAsync(caller, path);

        var calls = await StatusOfEachEnumValueAsync(client, url, EnumParametersOf(operation));

        calls.ShouldNotBeEmpty();
        calls.Where(call => call.Status != HttpStatusCode.OK).Select(call => $"{call.Request}: {call.Status}").ShouldBeEmpty();
    }

    private static async Task<List<(string Request, HttpStatusCode Status)>> StatusOfEachEnumValueAsync(
        HttpClient client,
        string url,
        IEnumerable<string> queries)
    {
        var calls = new List<(string, HttpStatusCode)>();
        foreach (var query in queries)
        {
            calls.Add(($"{url}?{query}", (await client.GetAsync($"{url}?{query}", Cancellation)).StatusCode));
        }

        return calls;
    }

    private static IEnumerable<(string Id, (string Path, JsonElement Operation) Value)> ListsOf(JsonElement document) =>
        document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (Id: operation.Value.GetProperty("operationId").GetString()!, Value: (path.Name, operation.Value))))
            .Where(list => list.Id.StartsWith("List", StringComparison.Ordinal))
            .Where(list => !list.Value.Value.GetProperty("parameters").EnumerateArray().Any(parameter => parameter.GetProperty("name").GetString() == "cursor"));

    // A caller who may read the list: a system admin, a user, or a member of a new tenant.
    private async Task<(HttpClient Client, string Url)> CallerAsync(string caller, string path) => caller switch
    {
        "platform" => (_api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true), path),
        "user" => (_api.CreateClient(await _testData.AddUserAsync()), path),
        _ => await MemberOfANewTenantAsync(path),
    };

    private async Task<(HttpClient Client, string Url)> MemberOfANewTenantAsync(string path)
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        return (_api.CreateClient(member), path.Replace("{tenantId}", tenant.Id.ToString(), StringComparison.Ordinal));
    }

    // Each value of each enum query parameter, as a query string.
    private IEnumerable<string> EnumParametersOf(JsonElement operation) =>
        operation.GetProperty("parameters").EnumerateArray()
            .Where(parameter => parameter.GetProperty("in").GetString() == "query")
            .Select(parameter => (Name: parameter.GetProperty("name").GetString()!, Schema: Resolve(parameter.GetProperty("schema"))))
            .Where(parameter => parameter.Schema.TryGetProperty("enum", out _))
            .SelectMany(parameter => parameter.Schema.GetProperty("enum").EnumerateArray()
                .Select(value => $"{parameter.Name}={Uri.EscapeDataString(value.GetString()!)}"));

    private JsonElement Resolve(JsonElement schema) =>
        schema.TryGetProperty("$ref", out var reference)
            ? _document.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1])
            : schema;
}
