using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Platform.Notifications;

// GET /v1/platform/notifications (U7): the platform's bell shows a system admin's platform notifications only, and only behind the system
// door.
public sealed class ListPlatformNotificationsTests(Database database) : IAsyncLifetime
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
    public async Task ListPlatformNotifications_ASystemAdmin_ShowsOnlyThePlatformsWithALinkToTheTenant()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await _testData.AddSystemAdminAsync();
        await _testData.AddMemberAsync(tenant.Id, admin);
        var userId = await _testData.UserIdOfAsync(admin);
        var platform = await _rows.AddAsync(userId, "tenant.purged", tenant.Id, "Platform");
        var ofTheTenant = await _rows.AddAsync(userId, "tenant.plan_changed", tenant.Id, "Tenant");
        var personal = await _rows.AddAsync(userId, "member.removed", null, "Personal");

        var page = await _api.CreateClient(admin, secondFactor: true).GetFromJsonAsync<JsonElement>("/v1/platform/notifications?limit=100", Cancellation);

        // Every system admin of the shared database hears the platform events of the other tests too, so the page may hold more.
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("id").GetGuid()).ShouldContain(platform);
        items.Select(item => item.GetProperty("id").GetGuid()).ShouldNotContain(ofTheTenant);
        items.Select(item => item.GetProperty("id").GetGuid()).ShouldNotContain(personal);
        items.Single(item => item.GetProperty("id").GetGuid() == platform).GetProperty("link").GetRawText()
            .ShouldBe($$"""{"kind":"platform_tenant","tenantId":"{{tenant.Id}}"}""");
    }

    [Fact]
    public async Task ListPlatformNotifications_AMemberWhoIsNoSystemAdmin_IsForbidden()
    {
        var listed = await _api.CreateClient(await _testData.AddUserAsync(), secondFactor: true).GetAsync("/v1/platform/notifications", Cancellation);

        listed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
