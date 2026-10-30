using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Me.Notifications;

// POST /v1/me/notifications/{notificationId}/read (U7): the reader opens one notification of any bell, and the unread count falls. Row level
// security shows the reader only their own, so another user's answers as one that does not exist.
public sealed class MarkNotificationReadTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly NotificationRows _rows = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task MarkNotificationRead_OneOfTheReaders_MarksItAndTheCountFalls()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var userId = await _testData.UserIdOfAsync(member);
        var opened = await _rows.AddAsync(userId, "tenant.plan_changed", tenant.Id, "Tenant");
        await _rows.AddAsync(userId, "tenant.plan_changed", tenant.Id, "Tenant");
        var client = _api.CreateClient(member);

        var marked = await client.PostAsync($"/v1/me/notifications/{opened}/read", null, Cancellation);
        var again = await client.PostAsync($"/v1/me/notifications/{opened}/read", null, Cancellation);

        (marked.StatusCode, again.StatusCode).ShouldBe((HttpStatusCode.NoContent, HttpStatusCode.NoContent));
        (await _rows.IsReadAsync(opened)).ShouldBeTrue();
        (await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications/unread-count", Cancellation)).GetProperty("unread").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task MarkNotificationRead_AnotherUsers_IsNotFoundAndChangesNothing()
    {
        var other = await _testData.UserIdOfAsync(await _testData.AddUserAsync());
        var theirs = await _rows.AddAsync(other, "member.removed", null, "Personal");

        var marked = await _api.CreateClient(await _testData.AddUserAsync()).PostAsync($"/v1/me/notifications/{theirs}/read", null, Cancellation);

        marked.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await marked.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString().ShouldBe("notification.not_found");
        (await _rows.IsReadAsync(theirs)).ShouldBeFalse();
    }
}
