using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Fixtures;

// The terms endpoints: the platform's versions, the person's gate, and the public version in force. The versions belong to no tenant, so a
// test of them runs on a database of its own (OwnTerms): one scheduled version at a time is the rule of the whole database.
internal static class TermsRequests
{
    public const string Link = "https://brand.example/terms/2026";

    // The day of the request in UTC, which a version's day is compared with.
    public static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    // POST /v1/platform/terms/versions creates a version, so it takes an idempotency key (I1).
    public static async Task<HttpResponseMessage> CreateTermsVersionAsync(
        this HttpClient client,
        DateOnly effectiveDate,
        string url = Link,
        string? summary = null,
        bool requiresAcceptance = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/platform/terms/versions")
        {
            Content = JsonContent.Create(new { effectiveDate, url, summary, requiresAcceptance }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // A version created as the platform creates it, by its id.
    public static async Task<Guid> AddTermsVersionAsync(
        this HttpClient client,
        DateOnly effectiveDate,
        string url = Link,
        string? summary = null,
        bool requiresAcceptance = true)
    {
        var created = await client.CreateTermsVersionAsync(effectiveDate, url, summary, requiresAcceptance);
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> UpdateTermsVersionAsync(
        this HttpClient client,
        Guid versionId,
        DateOnly effectiveDate,
        string url = Link,
        string? summary = null,
        bool requiresAcceptance = true) =>
        client.PatchAsJsonAsync($"/v1/platform/terms/versions/{versionId}", new { effectiveDate, url, summary, requiresAcceptance }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> CancelTermsVersionAsync(this HttpClient client, Guid versionId) =>
        client.PostAsync($"/v1/platform/terms/versions/{versionId}/cancel", null, TestContext.Current.CancellationToken);

    public static Task<JsonElement> TermsOfUseAsync(this HttpClient client) =>
        client.GetFromJsonAsync<JsonElement>("/v1/platform/terms", TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> TermsVersionPeopleAsync(this HttpClient client, Guid versionId, string query) =>
        client.GetAsync($"/v1/platform/terms/versions/{versionId}/people{query}", TestContext.Current.CancellationToken);

    public static Task<JsonElement> MyTermsAsync(this HttpClient client) =>
        client.GetFromJsonAsync<JsonElement>("/v1/me/terms", TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> AcceptTermsVersionAsync(this HttpClient client, Guid versionId, string url = Link) =>
        client.PostAsJsonAsync($"/v1/me/terms/versions/{versionId}/accept", new { url }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> DismissTermsNoticeAsync(this HttpClient client, Guid versionId) =>
        client.PostAsync($"/v1/me/terms/versions/{versionId}/dismiss", null, TestContext.Current.CancellationToken);

    // What the gate asks, as "action" or "action:version id".
    public static string AskOf(JsonElement terms) =>
        terms.GetProperty("version").ValueKind == JsonValueKind.Null
            ? terms.GetProperty("action").GetString()!
            : $"{terms.GetProperty("action").GetString()}:{terms.GetProperty("version").GetProperty("id").GetGuid()}";

    public static async Task<string?> CodeOfAsync(HttpResponseMessage answer) =>
        (await answer.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString();
}

// A database of its own, with its host, its rows and a system admin, for one test of the terms.
internal sealed class OwnTerms : IAsyncDisposable
{
    private OwnTerms(Database database, string name, ApiFactory api, TestData data, HttpClient admin, string adminId)
    {
        Database = database;
        Name = name;
        Api = api;
        Data = data;
        Admin = admin;
        AdminId = adminId;
    }

    public Database Database { get; }

    public string Name { get; }

    public ApiFactory Api { get; }

    public TestData Data { get; }

    public HttpClient Admin { get; }

    public string AdminId { get; }

    public static async Task<OwnTerms> CreateAsync(Database database)
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, name), configureServices: ApiFactory.RemoveAuditRetention);
        var data = new TestData(database, name);
        var adminId = await data.AddSystemAdminAsync();
        return new OwnTerms(database, name, api, data, api.CreateClient(adminId, secondFactor: true), adminId);
    }

    // A tenant user who may accept: a member of a tenant, active.
    public async Task<(string ExternalId, HttpClient Client)> AddTenantUserAsync(Guid tenantId, string? email = null)
    {
        var externalId = await Data.AddMemberAsync(tenantId, await Data.AddUserAsync(email: email ?? TestEmails.Of($"{Guid.NewGuid():N}@example.com"), name: "Ada Lovelace"));
        return (externalId, Api.CreateClient(externalId));
    }

    public Task<T?> ScalarAsync<T>(string sql) => Database.ScalarAsSuperuserAsync<T>(sql, Name);

    public async ValueTask DisposeAsync() => await Api.DisposeAsync();
}
