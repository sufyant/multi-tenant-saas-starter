using System.Net;
using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// E1, E3, K14: the limit audit.retention.days is the tenant's Activity log period: at least 30 days, or unlimited, and 365 in every plan
// after the migration. A missing value keeps every entry. A plan change and an override that shorten the period start the tenant's hold
// at the command's moment: the row's held_until is the moment of the command's audit entry plus 30 days.
public sealed class AuditRetentionTests(Database database) : IAsyncLifetime
{
    private const string Key = "audit.retention.days";

    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [MemberData(nameof(SeedPlanIds))]
    public async Task MigrateTheDatabase_ASeededPlan_KeepsTheActivityLogFor365Days(Guid planId)
    {
        var value = await database.ScalarAsSuperuserAsync<string>(
            $"SELECT concat_ws('|', maximum, unlimited) FROM control_plane.plan_entitlements WHERE plan_id = '{planId}' AND key = '{Key}'");

        value.ShouldBe("365|false");
    }

    public static TheoryData<Guid> SeedPlanIds => new() { SeedPlans.Free, SeedPlans.Pro };

    // R15: the database refuses a period under the floor too, also from a script or a manual change.
    [Fact]
    public async Task UpdateAPlanValue_UnderTheFloor_IsRefusedByTheDatabase()
    {
        var plan = await _testData.AddPlanAsync(new() { [Key] = 30 });

        var update = () => database.ScalarAsSuperuserAsync<object>(
            $"UPDATE control_plane.plan_entitlements SET maximum = 29 WHERE plan_id = '{plan}' AND key = '{Key}'");

        (await update.ShouldThrowAsync<PostgresException>()).ConstraintName.ShouldBe("ck_plan_entitlements_audit_retention_floor");
    }

    [Fact]
    public async Task UpdateAnOverride_UnderTheFloor_IsRefusedByTheDatabase()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync();
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenant.Id, Key, PlanRequests.Max(30)))).EnsureSuccessStatusCode();

        var update = () => database.ScalarAsSuperuserAsync<object>(
            $"UPDATE control_plane.entitlement_overrides SET maximum = 29 WHERE tenant_id = '{tenant.Id}' AND key = '{Key}'");

        (await update.ShouldThrowAsync<PostgresException>()).ConstraintName.ShouldBe("ck_entitlement_overrides_audit_retention_floor");
    }

    [Fact]
    public async Task CreatePlan_ARetentionUnderTheFloor_IsInvalidWithTheKey()
    {
        var admin = await AdminAsync();

        var created = await admin.CreatePlanAsync($"Plan {Guid.NewGuid():N}", new Dictionary<string, object> { [Key] = PlanRequests.Max(29) });

        (created.StatusCode, await PlanRequests.ProblemOfAsync(created)).ShouldBe((HttpStatusCode.BadRequest, ("plan.entitlement_invalid", Key)));
    }

    [Fact]
    public async Task UpdatePlan_ARetentionUnderTheFloor_IsInvalidWithTheKey()
    {
        var admin = await AdminAsync();
        var plan = await _testData.AddPlanAsync(new() { [Key] = 365 });

        var updated = await admin.UpdatePlanAsync(plan, $"Plan {Guid.NewGuid():N}", new Dictionary<string, object> { [Key] = PlanRequests.Max(29) });

        (updated.StatusCode, await PlanRequests.ProblemOfAsync(updated)).ShouldBe((HttpStatusCode.BadRequest, ("plan.entitlement_invalid", Key)));
    }

    [Fact]
    public async Task SetEntitlementOverride_ARetentionUnderTheFloor_IsInvalidWithTheKey()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync();

        var set = await admin.SetOverrideAsync(tenant.Id, Key, PlanRequests.Max(29));

        (set.StatusCode, await PlanRequests.ProblemOfAsync(set)).ShouldBe((HttpStatusCode.BadRequest, ("plan.entitlement_invalid", Key)));
    }

    // E3: an effective read answers a missing value as unlimited; the plan's own value stays not set.
    [Fact]
    public async Task GetTenantPlan_APlanWithoutTheRetention_IsUnlimited()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync(planId: await _testData.AddPlanAsync(new()));

        var entry = PlanRequests.EntitlementOf(await admin.TenantPlanAsync(tenant.Id), Key);

        entry.GetProperty("effective").GetProperty("unlimited").GetBoolean().ShouldBeTrue();
        entry.GetProperty("planValue").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task ChangeTenantPlan_ToAShorterPeriod_StartsTheHoldAtTheChangesMoment()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync();
        var plan = await ShortPlanAsync();

        (await _api.WaitingForMessagesAsync(() => admin.ChangeTenantPlanAsync(tenant.Id, plan))).EnsureSuccessStatusCode();

        (await HoldAsync(tenant.Id, "tenant.plan_changed")).ShouldBe("90|t|t");
    }

    [Fact]
    public async Task ChangeTenantPlan_ToALongerPeriod_WritesNoRow()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync(planId: await ShortPlanAsync());

        (await _api.WaitingForMessagesAsync(() => admin.ChangeTenantPlanAsync(tenant.Id, SeedPlans.Pro))).EnsureSuccessStatusCode();

        (await HoldAsync(tenant.Id, "tenant.plan_changed")).ShouldBeNull();
    }

    [Fact]
    public async Task SetEntitlementOverride_AShorterPeriod_StartsTheHoldAtTheOverridesMoment()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync();

        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenant.Id, Key, PlanRequests.Max(90)))).EnsureSuccessStatusCode();

        (await HoldAsync(tenant.Id, "tenant.entitlement_override_set")).ShouldBe("90|t|t");
    }

    // An unlimited override removed: the plan's 365 days apply again, which is shorter.
    [Fact]
    public async Task RemoveEntitlementOverride_ThePlansShorterPeriodAppliesAgain_StartsTheHoldAtTheRemovalsMoment()
    {
        var admin = await AdminAsync();
        var tenant = await _testData.AddTenantAsync();
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenant.Id, Key, PlanRequests.Unlimited))).EnsureSuccessStatusCode();

        (await _api.WaitingForMessagesAsync(() => admin.RemoveOverrideAsync(tenant.Id, Key))).EnsureSuccessStatusCode();

        (await HoldAsync(tenant.Id, "tenant.entitlement_override_removed")).ShouldBe("365|t|t");
    }

    // A plan of 90 days, which the seeded plans' 365 days make shorter.
    private Task<Guid> ShortPlanAsync() => _testData.AddPlanAsync(new() { [Key] = 90 });

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    // "days|no older bound|held_until is the moment of the command's platform entry plus 30 days": the tenant's first row holds every entry.
    private Task<string?> HoldAsync(Guid tenantId, string operation) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT concat_ws('|', days, held_from IS NULL, held_until = (
                SELECT occurred_at + interval '30 days' FROM audit.platform_entries WHERE target_id = '{tenantId}' AND operation = '{operation}'))
            FROM audit.tenant_retentions WHERE tenant_id = '{tenantId}'
            """);
}
