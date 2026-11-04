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
}
