using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SharedKernel;
using Tenancy;

namespace Api.IntegrationTests.Me.DataExports;

// POST /v1/me/data-exports/{exportId}/download: the API checks the signed-in person, then answers a link that works for 5 minutes and is
// never stored (section 7, "File storage"). Each download records user.data_export_downloaded.
public sealed class DownloadDataExportTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task DownloadDataExport_ReadyExport_AnswersALinkToTheFileForFiveMinutesAndRecordsTheDownload()
    {
        var user = await _testData.AddUserAsync(email: "ada@example.com");
        var userId = await _testData.UserIdOfAsync(user);
        var exportId = await DataExportRequests.ReadyAsync(_api, user);

        var asked = DateTimeOffset.UtcNow;
        var download = await _api.WaitingForMessagesAsync(() => DownloadAsync(user, exportId));
        var answered = DateTimeOffset.UtcNow;

        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        var link = await download.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        link.GetProperty("expiresAt").GetDateTimeOffset().ShouldBeInRange(asked.AddMinutes(5), answered.AddMinutes(5));
        using var http = new HttpClient();
        var file = await http.GetByteArrayAsync(link.GetProperty("url").GetString(), Cancellation);
        file.ShouldBe(await ObjectStorage.ReadAsync(FileKeys.UserExport(userId, exportId)));
        (await _audit.PlatformLogAsync(userId)).ShouldBe(
            $"user.data_export_requested|User|{userId}|User|{userId}|-, user.data_export_downloaded|User|{userId}|User|{userId}|-");
    }

    // The own_files policy shows no other user's file: another person's export is not found, and nothing is recorded (OWASP API1).
    [Fact]
    public async Task DownloadDataExport_AnotherUsersExport_IsNotFound()
    {
        var owner = await _testData.AddUserAsync(email: "ada@example.com");
        var other = await _testData.AddUserAsync(email: "grace@example.com");
        var exportId = await DataExportRequests.ReadyAsync(_api, owner);

        var download = await DownloadAsync(other, exportId);

        download.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await download.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString().ShouldBe("data_export.not_found");
    }

    // An export in preparation, failed or expired has no file to download; the screen reads its status.
    [Fact]
    public async Task DownloadDataExport_AnExportInPreparation_IsAConflict()
    {
        var user = await _testData.AddUserAsync(email: "ada@example.com");
        var userId = await _testData.UserIdOfAsync(user);
        var exportId = Guid.CreateVersion7();
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO control_plane.stored_files (id, user_id, module, purpose, object_key, content_type, status, created_at)
            VALUES ('{exportId}', '{userId}', 'control_plane', 'DataExport', 'exports/users/{userId}/{exportId}.zip', 'application/zip', 'Preparing', now())
            """);

        var download = await DownloadAsync(user, exportId);

        download.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await download.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString().ShouldBe("data_export.not_ready");
    }

    private Task<HttpResponseMessage> DownloadAsync(string user, Guid exportId) =>
        _api.CreateClient(user).PostAsync($"{DataExportRequests.Path}/{exportId}/download", content: null, Cancellation);
}
