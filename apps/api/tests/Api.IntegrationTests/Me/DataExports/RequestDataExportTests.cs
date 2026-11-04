using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SharedKernel;
using Tenancy;

namespace Api.IntegrationTests.Me.DataExports;

// POST /v1/me/data-exports: the person asks for a copy of their data (GDPR, Articles 15 and 20), and the worker prepares it in the
// background (docs/flows/personal-data-export.md). One export is in progress at a time. When it is ready, the person is told in the
// personal bell and by email, and can download it for 7 days.
public sealed class RequestDataExportTests(Database database) : IAsyncLifetime
{
    private const string Path = DataExportRequests.Path;

    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private readonly NotificationRows _notifications = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // The answer is the export in preparation; the worker then writes the file, and the export is ready until 7 days later.
    [Fact]
    public async Task RequestDataExport_AUser_AnswersItInPreparationThenMakesItReady()
    {
        var user = await _testData.AddUserAsync(email: "ada@example.com", firstName: "Ada", lastName: "Lovelace");

        var requested = await _api.WaitingForMessagesAsync(() => RequestAsync(user));

        requested.StatusCode.ShouldBe(HttpStatusCode.Created);
        var export = await requested.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        export.GetProperty("status").GetString().ShouldBe("preparing");
        var latest = await (await _api.CreateClient(user).GetAsync($"{Path}/latest", Cancellation)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (latest.GetProperty("id").GetGuid(), latest.GetProperty("status").GetString()).ShouldBe((export.GetProperty("id").GetGuid(), "ready"));
        (latest.GetProperty("expiresAt").GetDateTimeOffset() - latest.GetProperty("readyAt").GetDateTimeOffset()).ShouldBe(TimeSpan.FromDays(7));
        latest.GetProperty("sizeBytes").GetInt64().ShouldBeGreaterThan(0);
    }

    // The file: a ZIP with a plain README and one JSON file for each topic of every module, with the person's own data only. The person's
    // actions come from every tenant's log; the platform's log may already hold the request itself.
    [Fact]
    public async Task RequestDataExport_AMemberWhoActed_WritesTheirProfileMembershipsActivityAndNotificationsOnly()
    {
        var tenant = await _testData.AddTenantAsync(name: "Northwind Traders");
        var user = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: "ada@example.com", firstName: "Ada", lastName: "Lovelace"));
        var other = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: "grace@example.com", firstName: "Grace", lastName: "Hopper"));
        var (userId, otherId) = (await _testData.UserIdOfAsync(user), await _testData.UserIdOfAsync(other));
        await _audit.AddTenantEntryAsync(tenant.Id, DateTimeOffset.UtcNow, "User", userId, "member.invited", "Invitation", Guid.CreateVersion7());
        await _audit.AddTenantEntryAsync(tenant.Id, DateTimeOffset.UtcNow, "User", otherId, "member.invited", "Invitation", Guid.CreateVersion7());
        await _notifications.AddAsync(userId, "member.joined", tenant.Id, "Tenant");
        await _notifications.AddAsync(otherId, "member.joined", tenant.Id, "Tenant");

        var export = await (await _api.WaitingForMessagesAsync(() => RequestAsync(user))).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        using var zip = new ZipArchive(new MemoryStream(await ObjectStorage.ReadAsync(FileKeys.UserExport(userId, export.GetProperty("id").GetGuid()))));
        zip.Entries.Select(entry => entry.FullName).ShouldBe(
            ["README.txt", "activity.json", "invitations.json", "memberships.json", "notifications.json", "profile.json"]);
        var profile = Json(zip, "profile.json");
        (profile.GetProperty("email").GetString(), profile.GetProperty("firstName").GetString()).ShouldBe(("ada@example.com", "Ada"));
        Json(zip, "memberships.json").EnumerateArray().Select(membership => membership.GetProperty("organisation").GetString()).ShouldBe(["Northwind Traders"]);
        Json(zip, "activity.json").EnumerateArray()
            .Where(action => action.GetProperty("tenantId").ValueKind == JsonValueKind.String)
            .Select(action => (action.GetProperty("tenantId").GetGuid(), action.GetProperty("operation").GetString()))
            .ShouldBe([(tenant.Id, "member.invited")]);
        Json(zip, "notifications.json").GetArrayLength().ShouldBe(1);
        Text(zip, "README.txt").ShouldContain("Clerk");
        Text(zip).ShouldNotContain("grace@example.com");
    }

    // The person learns that it is ready in the personal bell and by email, with a link to Account and security, never to the file; the
    // request records user.data_export_requested by the user.
    [Fact]
    public async Task RequestDataExport_Ready_TellsThePersonByBellAndEmailAndRecordsTheRequest()
    {
        var user = await _testData.AddUserAsync(email: "ada@example.com", firstName: "Ada", lastName: "Lovelace");
        var userId = await _testData.UserIdOfAsync(user);

        await _api.WaitingForMessagesAsync(() => RequestAsync(user));

        (await _notifications.OfAsync(userId)).ShouldBe([NotificationRows.Row(userId, "user.data_export_ready", null, "Personal")]);
        var email = _api.Email.Sent.ShouldHaveSingleItem();
        (email.To, email.Subject).ShouldBe(("ada@example.com", "Your data file is ready"));
        email.Text.ShouldNotContain("exports/");
        (await _audit.PlatformLogAsync(userId)).ShouldBe($"user.data_export_requested|User|{userId}|User|{userId}|-");
    }

    // One export at a time: a second request while one is in preparation changes nothing.
    [Fact]
    public async Task RequestDataExport_OneInPreparation_IsAConflict()
    {
        var user = await _testData.AddUserAsync(email: "ada@example.com");
        var userId = await _testData.UserIdOfAsync(user);
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO control_plane.stored_files (id, user_id, module, purpose, object_key, content_type, status, created_at)
            VALUES ('{Guid.CreateVersion7()}', '{userId}', 'control_plane', 'DataExport', 'exports/users/{userId}/x.zip', 'application/zip', 'Preparing', now())
            """);

        var requested = await RequestAsync(user);

        requested.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await requested.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString().ShouldBe("data_export.in_progress");
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.stored_files WHERE user_id = '{userId}'")).ShouldBe(1);
    }

    private static JsonElement Json(ZipArchive zip, string name)
    {
        using var reader = new StreamReader(zip.GetEntry(name)!.Open());
        return JsonDocument.Parse(reader.ReadToEnd()).RootElement.Clone();
    }

    private static string Text(ZipArchive zip, string? name = null) =>
        string.Concat(zip.Entries.Where(entry => name is null || entry.FullName == name).Select(entry => new StreamReader(entry.Open()).ReadToEnd()));

    private Task<HttpResponseMessage> RequestAsync(string user) => DataExportRequests.RequestAsync(_api, user);
}
