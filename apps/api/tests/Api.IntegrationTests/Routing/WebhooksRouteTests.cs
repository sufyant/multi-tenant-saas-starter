using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Routing;

// No webhook reaches the API: nobody edits a user in Clerk, so nothing flows back. The webhooks route group is gone, and its old
// address answers as any route that does not exist.
public sealed class WebhooksRouteTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ReceiveClerkEvent_TheOldWebhookAddress_IsNotFound()
    {
        var response = await _api.CreateClient().PostAsJsonAsync("/v1/webhooks/clerk", new { type = "user.updated", data = new { id = "user_1" } }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString().ShouldBe("route.not_found");
    }
}
