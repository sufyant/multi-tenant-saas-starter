using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Notifications;

// GET /v1/tenants/{tenantId}/notifications (U7): the tenant web's bell shows this tenant's notifications and the reader's personal ones,
// never another tenant's, newest first, by a cursor, rendered in the reader's language (U2), with each person named at the read: a deleted
// one reads as "Deleted user". Each type leads to its link kind. A removed member no longer sees the tenant's.
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
            .ShouldBe(("tenant.plan_changed", "tenant_plans", "The plan of Acme Ltd changed", "Your organisation moved from Free to Pro."));
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

        page.GetProperty("items")[0].GetProperty("body").GetString().ShouldBe("Platform, rolünüzü Üye rolünden Yönetici rolüne değiştirdi.");
    }

    // K3: the notification keeps the Owner by user id only. The Owner's deletion erases their names, so the read names them "Deleted
    // user" in the reader's language, and no stored row ever held the name.
    [Theory]
    [InlineData("en", "Deleted user changed your role from Member to Admin.")]
    [InlineData("tr", "Silinmiş kullanıcı, rolünüzü Üye rolünden Yönetici rolüne değiştirdi.")]
    [InlineData("ar", "غيّر مستخدم محذوف دورك من عضو إلى مشرف.")]
    public async Task ListNotifications_TheActorWasDeletedSince_NamesThemDeletedUserInTheLanguage(string language, string body)
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(firstName: "Ada", lastName: "Lovelace"), role: "Owner");
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(language: language));
        var memberId = await _testData.UserIdOfAsync(member);
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).ChangeMemberRoleAsync(tenant.Id, memberId, "admin"));
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        (await _api.WaitingForMessagesAsync(async () => await admin.DeleteAsync($"/v1/platform/users/{await _testData.UserIdOfAsync(owner)}", Cancellation)))
            .EnsureSuccessStatusCode();

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications", Cancellation);

        page.GetProperty("items")[0].GetProperty("body").GetString().ShouldBe(body);
        (await database.ScalarAsSuperuserAsync<long>(
            "SELECT count(*) FROM notifications.notifications WHERE \"values\"::text LIKE '%Ada%' OR \"values\"::text LIKE '%Lovelace%'")).ShouldBe(0);
    }

    // The bell names a person as they are now: a member's new name reads in an older notification.
    [Fact]
    public async Task ListNotifications_TheActorRenamedThemselvesSince_NamesThemByTheirNameNow()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(firstName: "Ada", lastName: "Lovelace"), role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id);
        var memberId = await _testData.UserIdOfAsync(member);
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).ChangeMemberRoleAsync(tenant.Id, memberId, "admin"));
        await database.ScalarAsSuperuserAsync<object>($"UPDATE control_plane.users SET last_name = 'King' WHERE external_id = '{owner}'");

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications", Cancellation);

        page.GetProperty("items")[0].GetProperty("body").GetString().ShouldBe("Ada King changed your role from Member to Admin.");
    }

    // U2: each type leads to its kind; an override leads to the page of its key.
    [Theory]
    [InlineData("member.role_changed", """{"tenantName": "Acme", "oldRoleName": "member", "oldRoleNameBuiltIn": "true", "newRoleName": "admin", "newRoleNameBuiltIn": "true"}""", "tenant")]
    [InlineData("member.joined", """{"tenantName": "Acme", "memberName": "0199c000-0000-7000-8000-000000000001", "roleName": "member", "roleNameBuiltIn": "true"}""", "tenant_members")]
    [InlineData("tenant.plan_changed", """{"tenantName": "Acme", "oldPlanName": "Free", "newPlanName": "Pro"}""", "tenant_plan")]
    [InlineData("tenant.entitlement_override_set", """{"tenantName": "Acme", "keyName": "members.max", "value": "50"}""", "tenant_members")]
    [InlineData("tenant.entitlement_override_set", """{"tenantName": "Acme", "keyName": "roles.custom", "value": "on"}""", "tenant_roles")]
    [InlineData("tenant.entitlement_override_removed", """{"tenantName": "Acme", "keyName": "roles.custom.max", "value": "not_set"}""", "tenant_roles")]
    public async Task ListNotifications_EachType_LeadsToItsLinkKind(string type, string values, string link)
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _rows.AddAsync(await _testData.UserIdOfAsync(member), type, tenant.Id, "Tenant", values: values);

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications", Cancellation);

        page.GetProperty("items")[0].GetProperty("link").GetRawText().ShouldBe($$"""{"kind":"{{link}}","tenantId":"{{tenant.Id}}"}""");
    }

    [Fact]
    public async Task ListNotifications_APersonalNotification_LeadsNowhere()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _rows.AddAsync(await _testData.UserIdOfAsync(member), "member.removed", null, "Personal");

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications", Cancellation);

        page.GetProperty("items")[0].GetProperty("link").GetRawText().ShouldBe("""{"kind":"none","tenantId":null}""");
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
