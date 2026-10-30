using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Me.EmailPreferences;

// PUT /v1/me/email-preferences (U4, U5): the dispatcher sends a notification's email only where the catalog and the preferences allow it: a
// category's switch on and the main switch on. A required type sends its email anyway. A category that cannot reach the user is not kept,
// and a change is audited in the platform's log (K3).
public sealed class ChangeEmailPreferencesTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ChangeEmailPreferences_ThePlanOnForOneOwner_EmailsThatOwnerOfThePlanChangeAndNotTheOther()
    {
        var tenant = await _testData.AddTenantAsync();
        var (on, off) = (PlatformTenants.Email(), PlatformTenants.Email());
        var choosing = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: on), role: "Owner");
        await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: off), role: "Owner");

        var changed = await _api.WaitingForMessagesAsync(() => ChangeAsync(choosing, enabled: true, ("tenant_plans", true)));
        await _api.WaitingForMessagesAsync(async () =>
            await _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true).ChangeTenantPlanAsync(tenant.Id, await _testData.AddPlanAsync([])));

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await changed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("categories").EnumerateArray()
            .Select(category => (category.GetProperty("category").GetString(), category.GetProperty("enabled").GetBoolean()))
            .ShouldBe([("access", false), ("member_invitations", false), ("tenant_plans", true)]);
        _api.Email.Sent.Where(sent => sent.To == on || sent.To == off).Select(sent => (sent.To, sent.Subject.StartsWith("Acme Ltd is now on ", StringComparison.Ordinal))).ShouldBe([(on, true)]);
        (await _audit.PlatformLogAsync(await _testData.UserIdOfAsync(choosing))).ShouldNotBeNull().ShouldContain("user.email_preferences_changed");
    }

    [Fact]
    public async Task ChangeEmailPreferences_TheMainSwitchOffWithThePlanOn_EmailsNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var email = PlatformTenants.Email();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email), role: "Owner");

        await _api.WaitingForMessagesAsync(() => ChangeAsync(owner, enabled: false, ("tenant_plans", true)));
        await _api.WaitingForMessagesAsync(async () =>
            await _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true).ChangeTenantPlanAsync(tenant.Id, await _testData.AddPlanAsync([])));

        _api.Email.Sent.Where(sent => sent.To == email).ShouldBeEmpty();
    }

    [Fact]
    public async Task ChangeEmailPreferences_TheMainSwitchOff_StillEmailsTheRequiredRemoval()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var email = PlatformTenants.Email();
        var member = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email));

        await _api.WaitingForMessagesAsync(() => ChangeAsync(member, enabled: false, ("access", false)));
        await _api.WaitingForMessagesAsync(async () => await _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, await _testData.UserIdOfAsync(member)));

        _api.Email.Sent.Where(sent => sent.To == email).Select(sent => sent.Subject).ShouldBe(["You were removed from Acme Ltd"]);
    }

    [Fact]
    public async Task ChangeEmailPreferences_ACategoryThatCannotReachTheUser_IsNotKept()
    {
        var member = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id);

        await _api.WaitingForMessagesAsync(() => ChangeAsync(member, enabled: true, ("platform_tenants", true), ("tenant_plans", true)));

        (await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM notifications.email_preferences WHERE user_id = '{await _testData.UserIdOfAsync(member)}'")).ShouldBe(0);
    }

    private Task<HttpResponseMessage> ChangeAsync(string user, bool enabled, params (string Category, bool Enabled)[] categories) =>
        _api.CreateClient(user).PutAsJsonAsync(
            "/v1/me/email-preferences",
            new { enabled, categories = categories.Select(choice => new { category = choice.Category, enabled = choice.Enabled }) },
            Cancellation);
}
