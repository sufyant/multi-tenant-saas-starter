using ControlPlane.Application.DataExports.ExpireDataExport;
using ControlPlane.Application.DataExports.FailDataExport;
using SharedKernel;
using Tenancy;

namespace Api.IntegrationTests.Me.DataExports;

// The scheduled ends of an export (docs/flows/personal-data-export.md): after 7 days the API deletes the file and the export says expired,
// and an export still in preparation an hour after the request has failed, so the person can ask again. Each checks on arrival whether
// it still applies, because nothing cancels it. The test delivers each now, as Wolverine delivers it when its moment comes.
public sealed class ExpireDataExportTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // The row stays, with its expiry, so the screen says when the file was deleted.
    [Fact]
    public async Task ExpireDataExport_AReadyExport_DeletesTheFileAndSaysExpired()
    {
        var user = await _testData.AddUserAsync(email: "ada@example.com");
        var userId = await _testData.UserIdOfAsync(user);
        var exportId = await DataExportRequests.ReadyAsync(_api, user);

        await DataExportRequests.DeliverAsync(_api, new ExpireDataExportCommand(exportId, userId));

        (await ObjectStorage.KeysAsync(FileKeys.UserExports(userId))).ShouldBeEmpty();
        var latest = await DataExportRequests.LatestAsync(_api, user);
        latest.GetProperty("status").GetString().ShouldBe("expired");
        latest.GetProperty("expiresAt").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.String);
    }

    // A ready export is past its preparation: the timeout leaves it as it is.
    [Fact]
    public async Task FailDataExport_AReadyExport_ChangesNothing()
    {
        var user = await _testData.AddUserAsync(email: "ada@example.com");
        var exportId = await DataExportRequests.ReadyAsync(_api, user);

        await DataExportRequests.DeliverAsync(_api, new FailDataExportCommand(exportId, await _testData.UserIdOfAsync(user)));

        (await DataExportRequests.LatestAsync(_api, user)).GetProperty("status").GetString().ShouldBe("ready");
    }

    // An export still in preparation at its timeout failed for good; the person can ask again.
    [Fact]
    public async Task FailDataExport_AnExportStillInPreparation_FailsItAndLetsThePersonAskAgain()
    {
        var user = await _testData.AddUserAsync(email: "ada@example.com");
        var userId = await _testData.UserIdOfAsync(user);
        var exportId = Guid.CreateVersion7();
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO control_plane.stored_files (id, user_id, module, purpose, object_key, content_type, status, created_at)
            VALUES ('{exportId}', '{userId}', 'control_plane', 'DataExport', 'exports/users/{userId}/{exportId}.zip', 'application/zip', 'Preparing', now() - interval '1 hour')
            """);

        await DataExportRequests.DeliverAsync(_api, new FailDataExportCommand(exportId, userId));

        (await DataExportRequests.LatestAsync(_api, user)).GetProperty("status").GetString().ShouldBe("failed");
        (await DataExportRequests.RequestAsync(_api, user)).StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);
    }
}
