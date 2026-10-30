using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Notifications;

// GET /v1/tenants/{tenantId}/notifications (U7): the tenant web's bell shows this tenant's notifications and the reader's personal ones,
// never another tenant's, newest first, by a cursor, rendered in the reader's language (U2). A removed member no longer sees the tenant's.
public sealed class ListNotificationsTests(Database database) : IAsyncLifetime
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
    public async Task ListNotifications_TheTenantsBell_ShowsItsOwnAndThePersonalOnesNewestFirstAndNoOtherTenants()
    {
        var (tenant, other) = (await _testData.AddTenantAsync(), await _testData.AddTenantAsync());
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _testData.AddMemberAsync(other.Id, member);
        var userId = await _testData.UserIdOfAsync(member);
        // PostgreSQL keeps a moment to the microsecond.
        var at = new DateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(-10).UtcTicks / 10 * 10, TimeSpan.Zero);
        var own = await _rows.AddAsync(userId, "tenant.plan_changed", tenant.Id, "Tenant", createdAt: at);
        var personal = await _rows.AddAsync(userId, "member.removed", null, "Personal", "Globex", at.AddMinutes(1));
        await _rows.AddAsync(userId, "tenant.plan_changed", other.Id, "Tenant", "Initech", at.AddMinutes(2));
        await _rows.AddAsync(userId, "tenant.purged", other.Id, "Platform", "Initech", at.AddMinutes(3));

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications", Cancellation);

        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldBe([personal, own]);
        var item = page.GetProperty("items")[1];
        (item.GetProperty("type").GetString(), item.GetProperty("category").GetString(), item.GetProperty("title").GetString(), item.GetProperty("body").GetString())
            .ShouldBe(("tenant.plan_changed", "tenant_plans", "The plan of Acme Ltd changed", "Open the plan to see what Acme Ltd can use now."));
        item.GetProperty("link").GetRawText().ShouldBe($$"""{"kind":"tenant_plan","tenantId":"{{tenant.Id}}"}""");
        (item.GetProperty("createdAt").GetDateTimeOffset(), item.GetProperty("readAt").ValueKind).ShouldBe((at, JsonValueKind.Null));
    }

    [Fact]
    public async Task ListNotifications_AReaderInTurkish_RendersTheTextsInTurkish()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(language: "tr"));
        await _rows.AddAsync(await _testData.UserIdOfAsync(member), "member.role_changed", tenant.Id, "Tenant");

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications", Cancellation);

        page.GetProperty("items")[0].GetProperty("body").GetString().ShouldBe("Acme Ltd kuruluşunda artık Yönetici rolündesiniz.");
    }

    [Fact]
    public async Task ListNotifications_APageOfOne_AnswersACursorToTheNext()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var userId = await _testData.UserIdOfAsync(member);
        var older = await _rows.AddAsync(userId, "tenant.plan_changed", tenant.Id, "Tenant", createdAt: DateTimeOffset.UtcNow.AddMinutes(-2));
        await _rows.AddAsync(userId, "tenant.plan_changed", tenant.Id, "Tenant", createdAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var client = _api.CreateClient(member);

        var first = await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications?limit=1", Cancellation);
        var next = await client.GetFromJsonAsync<JsonElement>(
            $"/v1/tenants/{tenant.Id}/notifications?limit=1&cursor={first.GetProperty("nextCursor").GetString()}", Cancellation);

        next.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldBe([older]);
    }

    [Fact]
    public async Task ListNotifications_AMemberRemovedFromTheTenant_NoLongerReadsItsBell()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id);
        var userId = await _testData.UserIdOfAsync(member);
        await _rows.AddAsync(userId, "tenant.plan_changed", tenant.Id, "Tenant");
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, userId));

        var bell = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/notifications", Cancellation);
        var personal = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>("/v1/me/notifications", Cancellation);

        bell.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        personal.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("type").GetString()).ShouldBe(["member.removed"]);
    }
}
