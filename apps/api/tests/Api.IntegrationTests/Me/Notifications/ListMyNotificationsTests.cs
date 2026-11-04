using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Me.Notifications;

// GET /v1/me/notifications (U7): the personal bell. Its Unread tab reads only the unread ones, as the tenant's bell does.
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

    [Fact]
    public async Task ListMyNotifications_UnreadOnly_ShowsOnlyTheUnreadOnes()
    {
        var user = await _testData.AddUserAsync();
        var userId = await _testData.UserIdOfAsync(user);
        var unread = await _rows.AddAsync(userId, "member.removed", null, "Personal", createdAt: DateTimeOffset.UtcNow.AddMinutes(-2));
        var read = await _rows.AddAsync(userId, "member.removed", null, "Personal", createdAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await _rows.MarkReadAsync(read);

        var page = await _api.CreateClient(user).GetFromJsonAsync<JsonElement>("/v1/me/notifications?unread=true", Cancellation);

        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldBe([unread]);
    }
}
