using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// E1, K14: the limit audit.retention.days is the tenant's Activity log period: at least 30 days, or unlimited, and 365 in every plan
// after the migration, and the database refuses a period under the floor. The commands' tests sit in their own classes (N9).
public sealed class AuditRetentionTests(Database database) : IAsyncLifetime
{
    private const string Key = "audit.retention.days";

    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    public static TheoryData<Guid> SeedPlanIds => new() { SeedPlans.Free, SeedPlans.Pro };

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

        value.ShouldBe("365|f");
    }

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

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
