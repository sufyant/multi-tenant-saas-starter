using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Me.NotificationPreferences;

// PUT /v1/me/notification-preferences (U4, U5): the dispatcher sends each channel only where the catalog and the preferences allow it: the
// channel's main switch on, and the category's switch, or its default, on. A required category goes on every channel anyway, and a change
// to it is refused. With the web off no bell row is written, and the email still follows its own channel. A category that cannot reach the
// user is not kept; a kept choice stays when its category stops reaching the user. A change is audited in the platform's log (K3).
public sealed class ChangeNotificationPreferencesTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

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
    public async Task ChangeNotificationPreferences_ThePlansEmailOnForOneOwner_EmailsThatOwnerOfThePlanChangeAndNotTheOther()
    {
        var tenant = await _testData.AddTenantAsync();
        var (on, off) = (PlatformTenants.Email(), PlatformTenants.Email());
        var choosing = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: on), role: "Owner");
        await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: off), role: "Owner");

        var changed = await _api.WaitingForMessagesAsync(() => ChangeAsync(choosing, [], [("tenant_plans", "email", true)]));
        await ChangePlanAsync(tenant.Id);

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await changed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("categories").EnumerateArray()
            .Select(category => $"{category.GetProperty("category").GetString()} {Channels(category.GetProperty("channels"))}")
            .ShouldBe(["account_security web=True email=True", "access web=True email=False", "member_invitations web=True email=False", "tenant_plans web=True email=True"]);
        _api.Email.Sent.Where(sent => sent.To == on || sent.To == off).Select(sent => (sent.To, sent.Subject.StartsWith("Acme Ltd is now on ", StringComparison.Ordinal))).ShouldBe([(on, true)]);
        (await _audit.PlatformLogAsync(await _testData.UserIdOfAsync(choosing))).ShouldNotBeNull().ShouldContain("user.email_preferences_changed");
    }

    [Fact]
    public async Task ChangeNotificationPreferences_TheEmailsMainSwitchOffWithThePlanOn_EmailsNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var email = PlatformTenants.Email();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email), role: "Owner");

        await _api.WaitingForMessagesAsync(() => ChangeAsync(owner, [("email", false)], [("tenant_plans", "email", true)]));
        await ChangePlanAsync(tenant.Id);

        _api.Email.Sent.Where(sent => sent.To == email).ShouldBeEmpty();
    }

    [Fact]
    public async Task ChangeNotificationPreferences_TheWebsMainSwitchOff_WritesNoBellRowAndStillEmailsWhereTheEmailIsOn()
    {
        var tenant = await _testData.AddTenantAsync();
        var email = PlatformTenants.Email();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email), role: "Owner");

        await _api.WaitingForMessagesAsync(() => ChangeAsync(owner, [("web", false)], [("tenant_plans", "email", true)]));
        await ChangePlanAsync(tenant.Id);

        (await _rows.OfAsync(await _testData.UserIdOfAsync(owner))).ShouldBeEmpty();
        _api.Email.Sent.Where(sent => sent.To == email).Select(sent => sent.Subject.StartsWith("Acme Ltd is now on ", StringComparison.Ordinal)).ShouldBe([true]);
    }

    [Fact]
    public async Task ChangeNotificationPreferences_ThePlanOffOnTheWeb_WritesNoBellRowOfThePlan()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        await _api.WaitingForMessagesAsync(() => ChangeAsync(owner, [], [("tenant_plans", "web", false)]));
        await ChangePlanAsync(tenant.Id);

        (await _rows.OfAsync(await _testData.UserIdOfAsync(owner))).ShouldBeEmpty();
    }

    [Fact]
    public async Task ChangeNotificationPreferences_EveryMainSwitchOff_StillWritesAndEmailsTheRequiredRemoval()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var email = PlatformTenants.Email();
        var member = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email));
        var memberId = await _testData.UserIdOfAsync(member);

        await _api.WaitingForMessagesAsync(() => ChangeAsync(member, [("web", false), ("email", false)], [("access", "web", false)]));
        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, memberId));

        (await _rows.OfAsync(memberId)).ShouldBe([NotificationRows.Row(memberId, "member.removed", null, "Personal")]);
        _api.Email.Sent.Where(sent => sent.To == email).Select(sent => sent.Subject).ShouldBe(["You were removed from Acme Ltd"]);
    }

    [Fact]
    public async Task ChangeNotificationPreferences_TheRequiredCategory_IsRefusedAndKeepsNothing()
    {
        var member = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id);

        var refused = await _api.WaitingForMessagesAsync(() => ChangeAsync(member, [("email", false)], [("account_security", "email", false)]));

        refused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await refused.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString().ShouldBe("notification_preferences.category_required");
        (await StoredAsync(member)).ShouldBe(0);
    }

    [Fact]
    public async Task ChangeNotificationPreferences_ACategoryThatCannotReachTheUser_IsNotKept()
    {
        var member = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id);

        await _api.WaitingForMessagesAsync(() => ChangeAsync(member, [], [("platform_tenants", "email", true), ("tenant_plans", "web", false)]));

        (await StoredAsync(member)).ShouldBe(0);
    }

    [Fact]
    public async Task ChangeNotificationPreferences_ACategoryThatStopsReachingTheUser_KeepsTheChoice()
    {
        var tenant = await _testData.AddTenantAsync();
        var other = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _api.WaitingForMessagesAsync(() => ChangeAsync(owner, [], [("tenant_plans", "email", true)]));

        await _api.WaitingForMessagesAsync(async () => await _api.CreateClient(other).ChangeMemberRoleAsync(tenant.Id, await _testData.UserIdOfAsync(owner), "member"));

        var read = await _api.CreateClient(owner).GetFromJsonAsync<JsonElement>("/v1/me/notification-preferences", Cancellation);
        read.GetProperty("categories").EnumerateArray().Select(category => category.GetProperty("category").GetString()).ShouldBe(["account_security", "access"]);
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT categories::text FROM notifications.notification_preferences WHERE user_id = '{await _testData.UserIdOfAsync(owner)}'"))
            .ShouldBe("""{"tenant_plans": {"Email": true}}""");
    }

    private static string Channels(JsonElement channels) =>
        string.Join(' ', channels.EnumerateArray().Select(channel => $"{channel.GetProperty("channel").GetString()}={channel.GetProperty("enabled").GetBoolean()}"));

    private async Task ChangePlanAsync(Guid tenantId) =>
        await _api.WaitingForMessagesAsync(async () =>
            await _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true).ChangeTenantPlanAsync(tenantId, await _testData.AddPlanAsync([])));

    private async Task<long> StoredAsync(string user) =>
        await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM notifications.notification_preferences WHERE user_id = '{await _testData.UserIdOfAsync(user)}'");

    private Task<HttpResponseMessage> ChangeAsync(
        string user, (string Channel, bool Enabled)[] channels, (string Category, string Channel, bool Enabled)[] categories) =>
        _api.CreateClient(user).PutAsJsonAsync(
            "/v1/me/notification-preferences",
            new
            {
                channels = channels.Select(choice => new { channel = choice.Channel, enabled = choice.Enabled }),
                categories = categories.Select(choice => new { category = choice.Category, channel = choice.Channel, enabled = choice.Enabled }),
            },
            Cancellation);
}
