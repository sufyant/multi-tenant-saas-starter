using System.Net;
using Tenancy;

namespace Api.IntegrationTests.Me.Notifications;

// GET /v1/me/notifications/stream (U6): the signed-in user's live signals by server-sent events, in one process without a cache, the role
// all. A signal names only the bell that changed, never its content. A comment line keeps the stream open, and the stream closes after its
// lifetime and for a user who was suspended.
public sealed class StreamNotificationsTests(Database database) : IAsyncLifetime
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: new Dictionary<string, string?>
            {
                ["Notifications:LiveStream:KeepAliveInterval"] = "00:00:00.500",
                ["Notifications:LiveStream:Lifetime"] = "00:00:04",
            });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task StreamNotifications_ANotificationForTheUser_SendsOneSignalThatNamesTheBellAndNothingElse()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id);
        await using var stream = await LiveStream.OpenAsync(_api.CreateClient(member));

        await _api.WaitingForMessagesAsync(async () => await _api.CreateClient(owner).ChangeMemberRoleAsync(tenant.Id, await _testData.UserIdOfAsync(member), "admin"));

        (stream.Response.StatusCode, stream.Response.Content.Headers.ContentType?.MediaType).ShouldBe((HttpStatusCode.OK, "text/event-stream"));
        (await stream.NextEventAsync(Wait)).ShouldBe($$"""
            event: notifications
            data: {"scope":"tenant","tenantId":"{{tenant.Id}}"}
            """);
    }

    [Fact]
    public async Task StreamNotifications_ANotificationForAnotherUser_SendsOnlyCommentLines()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id);
        await using var stream = await LiveStream.OpenAsync(_api.CreateClient(owner));

        await _api.WaitingForMessagesAsync(async () => await _api.CreateClient(owner).ChangeMemberRoleAsync(tenant.Id, await _testData.UserIdOfAsync(member), "admin"));

        (await stream.NextAsync(Wait)).ShouldBe(LiveStream.KeepAlive);
        (await stream.NextAsync(Wait)).ShouldBe(LiveStream.KeepAlive);
    }

    [Fact]
    public async Task StreamNotifications_ItsLifetimeEnds_ClosesTheStream()
    {
        await using var stream = await LiveStream.OpenAsync(_api.CreateClient(await _testData.AddUserAsync()));

        var block = await stream.NextEventAsync(Wait);

        block.ShouldBe("closed");
    }

    [Fact]
    public async Task StreamNotifications_TheUserIsSuspended_ClosesAtTheNextSend()
    {
        var user = await _testData.AddUserAsync();
        await using var stream = await LiveStream.OpenAsync(_api.CreateClient(user));
        (await stream.NextAsync(Wait)).ShouldBe(LiveStream.KeepAlive);

        await _api.WaitingForMessagesAsync(async () =>
        {
            await _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true)
                .PostAsync($"/v1/platform/users/{await _testData.UserIdOfAsync(user)}/suspend", null, TestContext.Current.CancellationToken);
            return 0;
        });

        (await stream.NextAsync(Wait)).ShouldBe("closed");
    }
}
