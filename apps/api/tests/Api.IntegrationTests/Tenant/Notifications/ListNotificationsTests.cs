using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Notifications;

// GET /v1/tenants/{tenantId}/notifications (U7): the tenant web's bell shows this tenant's notifications and the reader's personal ones,
// never another tenant's, newest first, by a cursor, rendered in the reader's language (U2), with each person named at the read: a deleted
// one reads as "Deleted user". Each type leads to its link kind and names its area. An item names its actor and its organisation. The
// Unread tab reads only the unread ones. A removed member no longer sees the tenant's.
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
        (item.GetProperty("type").GetString(), item.GetProperty("title").GetString(), item.GetProperty("body").GetString())
            .ShouldBe(("tenant.plan_changed", "Acme Ltd is now on Pro", "The platform moved your organisation from Free to Pro. Its features and limits follow the new plan."));
        item.GetProperty("link").GetRawText().ShouldBe($$"""{"kind":"tenant_settings","tenantId":"{{tenant.Id}}"}""");
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
    [InlineData("en", "Deleted user changed your role from Member to Admin.", "Deleted user")]
    [InlineData("tr", "Silinmiş kullanıcı rolünüzü Üye rolünden Yönetici rolüne değiştirdi.", "Silinmiş kullanıcı")]
    [InlineData("ar", "غُيّر دورك من عضو إلى مشرف بواسطة مستخدم محذوف.", "مستخدم محذوف")]
    public async Task ListNotifications_TheActorWasDeletedSince_NamesThemDeletedUserInTheLanguage(string language, string body, string actor)
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

        var item = page.GetProperty("items")[0];
        (item.GetProperty("body").GetString(), item.GetProperty("actorName").GetString()).ShouldBe((body, actor));
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
    [InlineData("member.role_changed", """{"tenantName": "Acme", "oldRoleName": "member", "oldRoleNameBuiltIn": "true", "newRoleName": "admin", "newRoleNameBuiltIn": "true"}""", "tenant_settings")]
    [InlineData("member.joined", """{"tenantName": "Acme", "memberName": "0199c000-0000-7000-8000-000000000001", "roleName": "member", "roleNameBuiltIn": "true"}""", "tenant_members")]
    [InlineData("tenant.plan_changed", """{"tenantName": "Acme", "oldPlanName": "Free", "newPlanName": "Pro"}""", "tenant_settings")]
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
    public async Task ListNotifications_APersonalNotification_LeadsToTheStartPage()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _rows.AddAsync(await _testData.UserIdOfAsync(member), "member.removed", null, "Personal");

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications", Cancellation);

        page.GetProperty("items")[0].GetProperty("link").GetRawText().ShouldBe("""{"kind":"start","tenantId":null}""");
    }

    // The Unread tab: only the unread ones, newest first, paged by the same cursor. The read one between them is skipped.
    [Fact]
    public async Task ListNotifications_UnreadOnly_ShowsOnlyTheUnreadOnesPagedByTheCursor()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var userId = await _testData.UserIdOfAsync(member);
        var at = DateTimeOffset.UtcNow.AddMinutes(-10);
        var oldest = await _rows.AddAsync(userId, "tenant.plan_changed", tenant.Id, "Tenant", createdAt: at);
        var read = await _rows.AddAsync(userId, "member.removed", null, "Personal", createdAt: at.AddMinutes(1));
        var newest = await _rows.AddAsync(userId, "tenant.plan_changed", tenant.Id, "Tenant", createdAt: at.AddMinutes(2));
        await _rows.MarkReadAsync(read);
        var client = _api.CreateClient(member);

        var first = await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications?unread=true&limit=1", Cancellation);
        var next = await client.GetFromJsonAsync<JsonElement>(
            $"/v1/tenants/{tenant.Id}/notifications?unread=true&limit=1&cursor={first.GetProperty("nextCursor").GetString()}", Cancellation);

        first.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldBe([newest]);
        next.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldBe([oldest]);
        next.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // An item names its area in the reader's language, the person who acted as they are named now, and its organisation as it was, so the
    // list writes "Northwind Traders · Members" with no word of its own.
    [Theory]
    [InlineData("en", "Members")]
    [InlineData("tr", "Üyeler")]
    [InlineData("ar", "الأعضاء")]
    public async Task ListNotifications_ANewMember_NamesTheAreaInTheLanguageTheActorAndTheOrganisation(string language, string area)
    {
        var tenant = await _testData.AddTenantAsync();
        var joined = await _testData.UserIdOfAsync(await _testData.AddUserAsync(firstName: "Omar", lastName: "Haddad"));
        var member = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(language: language));
        await _rows.AddAsync(
            await _testData.UserIdOfAsync(member),
            "member.joined",
            tenant.Id,
            "Tenant",
            values: $$"""{"tenantName": "Northwind Traders", "memberName": "{{joined}}", "roleName": "member", "roleNameBuiltIn": "true"}""");

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications", Cancellation);

        var item = page.GetProperty("items")[0];
        (item.GetProperty("area").GetString(), item.GetProperty("actorName").GetString(), item.GetProperty("tenantName").GetString())
            .ShouldBe((area, "Omar Haddad", "Northwind Traders"));
    }

    // The area of each type of the tenant's bell: a role is the reader's access, and an override is a value of the plan.
    [Theory]
    [InlineData("member.role_changed", """{"tenantName": "Acme", "oldRoleName": "member", "oldRoleNameBuiltIn": "true", "newRoleName": "admin", "newRoleNameBuiltIn": "true"}""", "Your access")]
    [InlineData("member.joined", """{"tenantName": "Acme", "memberName": "0199c000-0000-7000-8000-000000000001", "roleName": "member", "roleNameBuiltIn": "true"}""", "Members")]
    [InlineData("tenant.plan_changed", """{"tenantName": "Acme", "oldPlanName": "Free", "newPlanName": "Pro"}""", "Organisation plan")]
    [InlineData("tenant.entitlement_override_set", """{"tenantName": "Acme", "keyName": "members.max", "value": "50"}""", "Organisation plan")]
    [InlineData("tenant.entitlement_override_removed", """{"tenantName": "Acme", "keyName": "roles.custom.max", "value": "not_set"}""", "Organisation plan")]
    public async Task ListNotifications_EachType_NamesItsArea(string type, string values, string area)
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _rows.AddAsync(await _testData.UserIdOfAsync(member), type, tenant.Id, "Tenant", values: values);

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications", Cancellation);

        page.GetProperty("items")[0].GetProperty("area").GetString().ShouldBe(area);
    }

    // A plan change and a role change by the platform name nobody: the item has no actor.
    [Theory]
    [InlineData("tenant.plan_changed")]
    [InlineData("member.role_changed")]
    public async Task ListNotifications_AnEventWithoutAPerson_AnswersNoActorName(string type)
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _rows.AddAsync(await _testData.UserIdOfAsync(member), type, tenant.Id, "Tenant");

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications", Cancellation);

        page.GetProperty("items")[0].GetProperty("actorName").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // A personal notification is about no tenant's bell, but it still names the organisation it tells of. A removal is the reader's access.
    [Fact]
    public async Task ListNotifications_APersonalNotification_NamesTheOrganisationItTellsOfAndTheArea()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _rows.AddAsync(await _testData.UserIdOfAsync(member), "member.removed", null, "Personal", "Globex");

        var page = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}/notifications", Cancellation);

        var item = page.GetProperty("items")[0];
        (item.GetProperty("tenantName").GetString(), item.GetProperty("area").GetString()).ShouldBe(("Globex", "Your access"));
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
