using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// PUT /v1/platform/tenants/{tenantId}/entitlement-overrides/{key}: a system admin gives one key of the code's list a value of its kind
// for one tenant, over its plan's value, with a reason, until an expiry if there is one. An override that expires stops at once, and
// stays, shown expired, until a system admin removes it.
public sealed class SetEntitlementOverrideTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private readonly FakeTimeProvider _time = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // A trial: roles.custom on, with five of them, for a Free tenant for fourteen days. The tenant's plan shows the override, with who set
    // it and when.
    [Fact]
    public async Task SetEntitlementOverride_AFeatureOnAFreeTenant_TurnsItOnAndShowsWhoSetIt()
    {
        var (admin, adminId) = await AdminAsync();
        var (tenantId, owner) = await PlanAnswers.FreeTenantAsync(_testData, _api);
        var expiresAt = _time.GetUtcNow().AddDays(14);

        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom.max", PlanRequests.Max(5), expiresAt))).EnsureSuccessStatusCode();
        var set = await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom", PlanRequests.On, expiresAt, "  A trial  "));
        var created = await owner.CreateRoleAsync(tenantId, "Billing", ["tenant.profile.read"]);

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        var entry = PlanRequests.EntitlementOf(await set.Content.ReadFromJsonAsync<JsonElement>(Cancellation), "roles.custom");
        entry.GetProperty("effective").GetProperty("enabled").GetBoolean().ShouldBeTrue();
        var stored = entry.GetProperty("override");
        (stored.GetProperty("state").GetString(), stored.GetProperty("reason").GetString(), stored.GetProperty("setById").GetGuid(), stored.GetProperty("expiresAt").GetDateTimeOffset())
            .ShouldBe(("active", "A trial", adminId, expiresAt));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // The override expires and the next request sees the plan's value: the effective value is computed at each read, with the moment.
    [Fact]
    public async Task SetEntitlementOverride_ItsExpiryPasses_StopsAtOnceAndStaysShownExpired()
    {
        var (admin, _) = await AdminAsync();
        var (tenantId, owner) = await PlanAnswers.FreeTenantAsync(_testData, _api);
        (await admin.SetOverrideAsync(tenantId, "roles.custom.max", PlanRequests.Max(5))).EnsureSuccessStatusCode();
        (await admin.SetOverrideAsync(tenantId, "roles.custom", PlanRequests.On, _time.GetUtcNow().AddMinutes(1))).EnsureSuccessStatusCode();
        var before = await owner.CreateRoleAsync(tenantId, "Billing", ["tenant.profile.read"]);

        _time.Advance(TimeSpan.FromMinutes(2));
        var after = await owner.CreateRoleAsync(tenantId, "Support", ["tenant.profile.read"]);

        (before.StatusCode, after.StatusCode).ShouldBe((HttpStatusCode.Created, HttpStatusCode.Forbidden));
        var entry = PlanRequests.EntitlementOf(await admin.TenantPlanAsync(tenantId), "roles.custom");
        (entry.GetProperty("override").GetProperty("state").GetString(), entry.GetProperty("effective").GetProperty("enabled").GetBoolean()).ShouldBe(("expired", false));
    }

    [Theory]
    [InlineData("projects.max")]
    [InlineData("members.max")]
    public async Task SetEntitlementOverride_AKeyNotOnTheListOrAValueOfTheWrongKind_IsInvalidWithTheKey(string key)
    {
        var (admin, _) = await AdminAsync();
        var tenant = await _testData.AddTenantAsync();

        var set = await admin.SetOverrideAsync(tenant.Id, key, PlanRequests.On);

        set.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PlanRequests.ProblemOfAsync(set)).ShouldBe(("plan.entitlement_invalid", key));
    }

    [Fact]
    public async Task SetEntitlementOverride_WithoutAReason_IsAValidationProblem()
    {
        var (admin, _) = await AdminAsync();
        var tenant = await _testData.AddTenantAsync();

        var set = await admin.SetOverrideAsync(tenant.Id, "members.max", PlanRequests.Unlimited, reason: "  ");

        (set.StatusCode, (await PlanRequests.ProblemOfAsync(set)).Code).ShouldBe((HttpStatusCode.BadRequest, "request.validation_failed"));
    }

    // A closed tenant's overrides are read-only, as its other platform changes are; a suspended tenant's can change.
    [Fact]
    public async Task SetEntitlementOverride_Closed_IsAConflict()
    {
        var (admin, _) = await AdminAsync();
        var tenant = await _testData.AddTenantAsync(status: "Closed", closedAt: _time.GetUtcNow());

        var set = await admin.SetOverrideAsync(tenant.Id, "members.max", PlanRequests.Unlimited);

        (set.StatusCode, (await PlanRequests.ProblemOfAsync(set)).Code).ShouldBe((HttpStatusCode.Conflict, "tenant.not_active"));
    }

    [Fact]
    public async Task SetEntitlementOverride_Suspended_SetsIt()
    {
        var (admin, _) = await AdminAsync();
        var tenant = await _testData.AddTenantAsync(status: "Suspended", suspensionReason: "Unpaid invoices");

        var set = await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenant.Id, "members.max", PlanRequests.Max(50)));

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Both logs; the reason is the provider's own note, so only the platform's entry keeps it, as for tenant.suspended.
    [Fact]
    public async Task SetEntitlementOverride_AnOverride_RecordsItInBothLogsWithTheReasonInThePlatformsOnly()
    {
        var (admin, adminId) = await AdminAsync();
        var tenant = await _testData.AddTenantAsync(name: "Acme Ltd");

        await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenant.Id, "members.max", PlanRequests.Max(50), reason: "Seats for the pilot"));

        (await _audit.PlatformLogAsync(tenant.Id)).ShouldBe($"tenant.entitlement_override_set|User|{adminId}|Tenant|{tenant.Id}|Acme Ltd");
        (await _audit.PlatformDetailsAsync(tenant.Id)).ShouldBe(
            """{"key": "members.max", "reason": "Seats for the pilot", "newValue": {"enabled": null, "maximum": 50, "unlimited": false}, "oldValue": null, "newExpiresAt": null, "oldExpiresAt": null}""");
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe($"{tenant.Id}:tenant.entitlement_override_set|Platform|-|Tenant|{tenant.Id}|Acme Ltd");
        (await _audit.TenantDetailsAsync(tenant.Id)).ShouldBe(
            """{"key": "members.max", "reason": null, "newValue": {"enabled": null, "maximum": 50, "unlimited": false}, "oldValue": null, "newExpiresAt": null, "oldExpiresAt": null}""");
    }

    private async Task<(HttpClient Client, Guid UserId)> AdminAsync()
    {
        var admin = await _testData.AddSystemAdminAsync();
        return (_api.CreateClient(admin, secondFactor: true), await _testData.UserIdOfAsync(admin));
    }
}
