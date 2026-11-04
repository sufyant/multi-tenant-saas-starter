using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Me.DataExports;

// GET /v1/me/data-exports/latest: the person's latest export, which Account and security shows: in preparation, ready until a date,
// failed or expired. The cases with an export are in the tests of the steps that make them.
public sealed class GetMyDataExportTests(Database database) : IAsyncLifetime
{
    private const string Path = DataExportRequests.Path;

    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // A person who never asked has none: the screen offers "Export my data".
    [Fact]
    public async Task GetMyDataExport_NeverAsked_IsNotFound()
    {
        var user = await _testData.AddUserAsync(email: "ada@example.com");

        var latest = await _api.CreateClient(user).GetAsync($"{Path}/latest", Cancellation);

        latest.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await latest.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString().ShouldBe("data_export.not_found");
    }
}
