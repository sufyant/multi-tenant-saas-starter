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

    [Fact]
    public async Task SetEntitlementOverride_ARetentionUnderTheFloor_IsInvalidWithTheKey()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = await _testData.AddTenantAsync();

        var set = await admin.SetOverrideAsync(tenant.Id, "audit.retention.days", PlanRequests.Max(29));

        (set.StatusCode, await PlanRequests.ProblemOfAsync(set)).ShouldBe((HttpStatusCode.BadRequest, ("plan.entitlement_invalid", "audit.retention.days")));
    }

    [Fact]
    public async Task SetEntitlementOverride_AShorterPeriod_StartsTheHoldAtTheOverridesMoment()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var tenant = await _testData.AddTenantAsync();

        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenant.Id, "audit.retention.days", PlanRequests.Max(90)))).EnsureSuccessStatusCode();

        (await new AuditEntries(database).RetentionHoldAsync(tenant.Id, "tenant.entitlement_override_set")).ShouldBe("90|t|t");
    }

    // A trial: roles.custom on, with five of them, for a Free tenant for fourteen days. The tenant's plan shows the override, with who set
    // it and when.
    [Fact]
    public async Task SetEntitlementOverride_AFeatureOnAFreeTenant_TurnsItOnAndShowsWhoSetIt()
    {
        var (admin, adminId) = await AdminAsync();
        var (tenantId, owner) = await PlanAnswers.FreeTenantAsync(_testData, _api);
        var expiresAt = _time.GetUtcNow().AddDays(14);

        var set = await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom", PlanRequests.On, expiresAt, "  A trial  "));
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom.max", PlanRequests.Max(5), expiresAt))).EnsureSuccessStatusCode();
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
        (await admin.SetOverrideAsync(tenantId, "roles.custom", PlanRequests.On, _time.GetUtcNow().AddMinutes(1))).EnsureSuccessStatusCode();
        (await admin.SetOverrideAsync(tenantId, "roles.custom.max", PlanRequests.Max(5))).EnsureSuccessStatusCode();
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

    // E1: an override of a limit that depends on a feature is set only while the tenant's effective value of the feature is on.
    [Fact]
    public async Task SetEntitlementOverride_ADependentLimitWithTheFeatureOff_IsInvalidWithTheLimitAndSetsNothing()
    {
        var (admin, _) = await AdminAsync();
        var (tenantId, _) = await PlanAnswers.FreeTenantAsync(_testData, _api);

        var set = await admin.SetOverrideAsync(tenantId, "roles.custom.max", PlanRequests.Max(5));

        (set.StatusCode, await PlanRequests.ProblemOfAsync(set)).ShouldBe((HttpStatusCode.BadRequest, ("plan.feature_required", "roles.custom.max")));
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.entitlement_overrides WHERE tenant_id = '{tenantId}'")).ShouldBe(0);
    }

    [Fact]
    public async Task SetEntitlementOverride_ADependentLimitWithTheFeatureOn_SetsIt()
    {
        var (admin, _) = await AdminAsync();
        var (tenantId, _) = await PlanAnswers.TenantWithMembersMaxAsync(_testData, _api, membersMax: 5);

        var set = await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom.max", PlanRequests.Max(30)));

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        PlanRequests.EntitlementOf(await set.Content.ReadFromJsonAsync<JsonElement>(Cancellation), "roles.custom.max").GetProperty("effective").GetProperty("maximum").GetInt32().ShouldBe(30);
    }

    // The effective value counts: a feature override that turns the feature on lets a limit override be set on a plan without the feature.
    [Fact]
    public async Task SetEntitlementOverride_AFeatureOverrideThatTurnsTheFeatureOn_LetsALimitOverrideBeSet()
    {
        var (admin, _) = await AdminAsync();
        var (tenantId, _) = await PlanAnswers.FreeTenantAsync(_testData, _api);
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom", PlanRequests.On))).EnsureSuccessStatusCode();

        var set = await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom.max", PlanRequests.Max(5)));

        set.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // An expired feature override no longer counts: the plan's value, off, applies (E3).
    [Fact]
    public async Task SetEntitlementOverride_AFeatureOverrideThatExpired_RefusesALimitOverride()
    {
        var (admin, _) = await AdminAsync();
        var (tenantId, _) = await PlanAnswers.FreeTenantAsync(_testData, _api);
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom", PlanRequests.On, _time.GetUtcNow().AddMinutes(1)))).EnsureSuccessStatusCode();
        _time.Advance(TimeSpan.FromMinutes(2));

        var set = await admin.SetOverrideAsync(tenantId, "roles.custom.max", PlanRequests.Max(5));

        (set.StatusCode, await PlanRequests.ProblemOfAsync(set)).ShouldBe((HttpStatusCode.BadRequest, ("plan.feature_required", "roles.custom.max")));
    }

    private async Task<(HttpClient Client, Guid UserId)> AdminAsync()
    {
        var admin = await _testData.AddSystemAdminAsync();
        return (_api.CreateClient(admin, secondFactor: true), await _testData.UserIdOfAsync(admin));
    }
}
