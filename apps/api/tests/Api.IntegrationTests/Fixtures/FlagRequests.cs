using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Fixtures;

// The feature flag endpoints. The template's code defines no flag, so a test stores its flags as a deploy's sync would, with SQL. The list
// and the previews read every flag and every organisation of the database, so a test of them runs on a database of its own (OwnFlags).
internal static class FlagRequests
{
    public static Task<HttpResponseMessage> FeatureFlagsAsync(this HttpClient client) =>
        client.GetAsync("/v1/platform/feature-flags", TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> FeatureFlagAsync(this HttpClient client, string key) =>
        client.GetAsync($"/v1/platform/feature-flags/{key}", TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> AddFeatureFlagTenantAsync(this HttpClient client, string key, Guid tenantId) =>
        client.PutAsync($"/v1/platform/feature-flags/{key}/tenants/{tenantId}", null, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> RemoveFeatureFlagTenantAsync(this HttpClient client, string key, Guid tenantId) =>
        client.DeleteAsync($"/v1/platform/feature-flags/{key}/tenants/{tenantId}", TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> SetFeatureFlagPercentageAsync(this HttpClient client, string key, int percentage) =>
        client.PutAsJsonAsync($"/v1/platform/feature-flags/{key}/percentage", new { percentage }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> SetFeatureFlagEveryoneElseAsync(this HttpClient client, string key, bool on) =>
        client.PutAsJsonAsync($"/v1/platform/feature-flags/{key}/everyone-else", new { on }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> PreviewFeatureFlagChangeAsync(this HttpClient client, string key, object change) =>
        client.PostAsJsonAsync($"/v1/platform/feature-flags/{key}/preview", change, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> TenantFeatureFlagsAsync(this HttpClient client, Guid tenantId) =>
        client.GetAsync($"/v1/platform/tenants/{tenantId}/feature-flags", TestContext.Current.CancellationToken);

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage answer)
    {
        answer.EnsureSuccessStatusCode();
        return await answer.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    public static async Task<string?> CodeOfAsync(HttpResponseMessage answer) =>
        (await answer.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString();

    public static string Key() => $"test.flag_{Guid.NewGuid():N}";
}

// A database of its own, with its host and a system admin, for one test of the feature flags.
internal sealed class OwnFlags : IAsyncDisposable
{
    private OwnFlags(Database database, string name, ApiFactory api, TestData data, HttpClient admin)
    {
        Database = database;
        Name = name;
        Api = api;
        Data = data;
        Admin = admin;
    }

    public Database Database { get; }

    public string Name { get; }

    public ApiFactory Api { get; }

    public TestData Data { get; }

    public HttpClient Admin { get; }

    // flags runs before the host starts, so the worker's job at the start sees what it stores.
    public static async Task<OwnFlags> CreateAsync(Database database, Func<string, Task>? flags = null)
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var data = new TestData(database, name);
        var adminId = await data.AddSystemAdminAsync();
        if (flags is not null)
        {
            await flags(name);
        }

        var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, name), configureServices: ApiFactory.RemoveAuditRetention);
        return new OwnFlags(database, name, api, data, api.CreateClient(adminId, secondFactor: true));
    }

    // A flag as a deploy's sync stores it, with its rules and its first line of history at this moment, or days before it.
    public static Task AddFlagAsync(Database database, string name, string key, string type = "Release", bool everyoneElse = false, int daysAgo = 0, bool removed = false) =>
        database.ScalarAsSuperuserAsync<object>(
            $"""
            WITH flag AS (
              INSERT INTO control_plane.feature_flags (id, key, type, default_value, description, added_at, added_revision, removed_at, removed_revision, listed_count, percentage, everyone_else, state_since, last_changed_at)
              VALUES (gen_random_uuid(), '{key}', '{type}', {(type == "KillSwitch" ? "true" : "false")}, 'A test flag.', now() - interval '{daysAgo} days', '7c41d09',
                {(removed ? "now()" : "NULL")}, {(removed ? "'abc1234'" : "NULL")}, 0, 0, {(everyoneElse ? "true" : "false")}, now() - interval '{daysAgo} days', now() - interval '{daysAgo} days')
              RETURNING id, added_at
            )
            INSERT INTO control_plane.feature_flag_changes (id, flag_id, kind, at, revision) SELECT gen_random_uuid(), id, 'AddedToCode', added_at, '7c41d09' FROM flag
            """,
            name);

    public Task AddFlagAsync(string key, string type = "Release", bool everyoneElse = false, int daysAgo = 0, bool removed = false) =>
        AddFlagAsync(Database, Name, key, type, everyoneElse, daysAgo, removed);

    public Task<T?> ScalarAsync<T>(string sql) => Database.ScalarAsSuperuserAsync<T>(sql, Name);

    public async ValueTask DisposeAsync() => await Api.DisposeAsync();
}
