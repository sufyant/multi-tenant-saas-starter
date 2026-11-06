using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Me.Notifications;

// GET /v1/me/notifications (U7): the personal bell. Its filter reads only the unread ones, or only the read ones, as the tenant's bell does.
public sealed class ListMyNotificationsTests(Database database) : IAsyncLifetime
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ListMyNotifications_TheUnreadFilter_ShowsOnlyTheItemsOfThatValue(bool unreadOnly)
    {
        var user = await _testData.AddUserAsync();
        var userId = await _testData.UserIdOfAsync(user);
        var unread = await _rows.AddAsync(userId, "member.removed", null, "Personal", createdAt: DateTimeOffset.UtcNow.AddMinutes(-2));
        var read = await _rows.AddAsync(userId, "member.removed", null, "Personal", createdAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await _rows.MarkReadAsync(read);

        var page = await _api.CreateClient(user).GetFromJsonAsync<JsonElement>($"/v1/me/notifications?unread={unreadOnly.ToString().ToLowerInvariant()}", Cancellation);

        page.GetProperty("items").EnumerateArray().Select(item => (item.GetProperty("id").GetGuid() == unread, item.GetProperty("unread").GetBoolean()))
            .ShouldBe([(unreadOnly, unreadOnly)]);
    }

    // The Owners' reactivated and reopened lead into the organisation; suspended, closed and purged to the start page (the maintainer's
    // decisions of 2026-11-06). The personal bell shows both, and the platform names no system admin.
    [Fact]
    public async Task ListMyNotifications_AnOwnersReopenedAndSuspendedOrganisation_LeadToItsSettingsAndToTheStartPage()
    {
        var user = await _testData.AddUserAsync();
        var userId = await _testData.UserIdOfAsync(user);
        var tenantId = Guid.CreateVersion7();
        await _rows.AddAsync(userId, "tenant.suspended_for_owners", null, "Personal", createdAt: DateTimeOffset.UtcNow.AddMinutes(-2));
        await _rows.AddAsync(userId, "tenant.reopened_for_owners", tenantId, "Personal", createdAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var page = await _api.CreateClient(user).GetFromJsonAsync<JsonElement>("/v1/me/notifications", Cancellation);

        page.GetProperty("items").EnumerateArray()
            .Select(item => string.Join('|', item.GetProperty("title").GetString(), item.GetProperty("body").GetString(), item.GetProperty("link").GetRawText()))
            .ShouldBe(
            [
                $$"""Acme Ltd was reopened|The platform reopened Acme Ltd. Its members can open it again.|{"kind":"tenant_settings","tenantId":"{{tenantId}}"}""",
                """Acme Ltd was suspended|The platform suspended Acme Ltd. Its members can’t open it until it is reactivated.|{"kind":"start","tenantId":null}""",
            ]);
    }
}
