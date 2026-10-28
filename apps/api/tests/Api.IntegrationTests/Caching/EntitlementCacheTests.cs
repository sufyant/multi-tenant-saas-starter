using System.Net;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Caching;

// Section 3, "Cache": the feature check reads a tenant's entitlements from Valkey: the tenant's plan and overrides under the tenant's tag,
// the plan's values under the plan's tag. A plan edit removes the plan's entry, so every tenant on it sees the edit at once, and a tenant
// on another plan keeps its entries. The effective value is computed at each read, so an override that expires stops at once, with no
// removal. Each test reads first, so the old answer is in the cache, then writes, then reads again.
public sealed class EntitlementCacheTests(Valkey valkey, Database database) : IClassFixture<Valkey>, IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    // The clock starts at the real now, so no message a write schedules comes due during a run (section 9, rule 10).
    private readonly FakeTimeProvider _time = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));

    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            time: _time,
            settings: new Dictionary<string, string?> { ["ConnectionStrings:Cache"] = valkey.ConnectionString });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task CheckFeature_AfterThePlanIsEdited_SeesTheNewValueAtOnce()
    {
        var admin = await AdminAsync();
        var planId = await _testData.AddPlanAsync(new() { ["roles.custom"] = false, ["roles.custom.max"] = 5 });
        var (tenantId, owner) = await OwnerAsync(planId);
        var before = await owner.CreateRoleAsync(tenantId, "Billing", ["tenant.profile.read"]);

        (await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(planId, $"Plan {planId:N}", new Dictionary<string, object>
        {
            ["roles.custom"] = PlanRequests.On,
            ["roles.custom.max"] = PlanRequests.Max(5),
        }))).EnsureSuccessStatusCode();
        var after = await owner.CreateRoleAsync(tenantId, "Billing", ["tenant.profile.read"]);

        (before.StatusCode, after.StatusCode).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.Created));
    }

    // The same request of a tenant on another plan, a role update that changes nothing, reads the database exactly as much before and
    // after the edit: its entries stay.
    [Fact]
    public async Task CheckFeature_AfterAnotherPlanIsEdited_KeepsTheTenantsEntries()
    {
        var admin = await AdminAsync();
        var edited = await _testData.AddPlanAsync(new() { ["roles.custom"] = true, ["roles.custom.max"] = 5 });
        var kept = await _testData.AddPlanAsync(new() { ["roles.custom"] = true, ["roles.custom.max"] = 5 });
        var tenant = await _testData.AddTenantAsync(planId: kept);
        var tenantId = tenant.Id;
        var owner = await _testData.AddMemberAsync(tenantId, role: "Owner");
        var role = await _testData.AddCustomRoleAsync(tenantId, "Billing", "tenant.profile.read");
        using var commands = new SqlCommands();
        var readsBefore = await commands.CountAsync(() => _api.CreateClient(owner), client => client.UpdateRoleAsync(tenantId, role, "Billing", ["tenant.profile.read"]));

        (await _api.WaitingForMessagesAsync(() => admin.UpdatePlanAsync(edited, $"Plan {edited:N}", new Dictionary<string, object>()))).EnsureSuccessStatusCode();
        var readsAfter = await commands.CountAsync(() => _api.CreateClient(owner), client => client.UpdateRoleAsync(tenantId, role, "Billing", ["tenant.profile.read"]));

        readsAfter.ShouldBe(readsBefore);
    }

    [Fact]
    public async Task CheckFeature_AfterTheTenantsPlanChanges_SeesTheNewPlanAtOnce()
    {
        var admin = await AdminAsync();
        var (tenantId, owner) = await OwnerAsync(SeedPlans.Pro);
        var before = await owner.CreateRoleAsync(tenantId, "Billing", ["tenant.profile.read"]);

        (await _api.WaitingForMessagesAsync(() => admin.ChangeTenantPlanAsync(tenantId, SeedPlans.Free))).EnsureSuccessStatusCode();
        var after = await owner.CreateRoleAsync(tenantId, "Support", ["tenant.profile.read"]);

        (before.StatusCode, after.StatusCode).ShouldBe((HttpStatusCode.Created, HttpStatusCode.Forbidden));
    }

    // The entry holds the override with its expiry, and the moment of each read decides: no removal, no wait for the entry to expire.
    [Fact]
    public async Task CheckFeature_AnOverrideExpires_SeesThePlansValueAtOnce()
    {
        var admin = await AdminAsync();
        var (tenantId, owner) = await OwnerAsync(SeedPlans.Free);
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom.max", PlanRequests.Max(5)))).EnsureSuccessStatusCode();
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenantId, "roles.custom", PlanRequests.On, _time.GetUtcNow().AddMinutes(1)))).EnsureSuccessStatusCode();
        var before = await owner.CreateRoleAsync(tenantId, "Billing", ["tenant.profile.read"]);

        _time.Advance(TimeSpan.FromMinutes(1));
        var after = await owner.CreateRoleAsync(tenantId, "Support", ["tenant.profile.read"]);

        (before.StatusCode, after.StatusCode).ShouldBe((HttpStatusCode.Created, HttpStatusCode.Forbidden));
    }

    private async Task<(Guid TenantId, HttpClient Owner)> OwnerAsync(Guid planId)
    {
        var tenant = await _testData.AddTenantAsync(planId: planId);
        return (tenant.Id, _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner")));
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
