using System.Diagnostics.Metrics;
using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Tenants;
using ControlPlane.Infrastructure.Persistence;
using ControlPlane.Infrastructure.Persistence.Plans;
using ControlPlane.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tenancy.Caching;

namespace ControlPlane.IntegrationTests.Persistence.Plans;

// The effective value of each key: the plan's value, overlaid by the tenant's override while it has not expired; a key the plan has no
// value for is closed. The directory computes it at each call, so an override that expired stops at once.
public sealed class EntitlementDirectoryTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindEntitlements_ATenantOnTheDefaultPlan_AreTheDefaultPlansValues()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);

        var found = await FindAsync(tenant);

        found.Features.ShouldBe(new Dictionary<string, bool> { ["roles.custom"] = false });
        found.Limits.ShouldBe(new Dictionary<string, int?> { ["members.max"] = 3, ["roles.custom.max"] = 0, ["audit.retention.days"] = 365 }, ignoreOrder: true);
    }

    [Fact]
    public async Task FindEntitlements_AnOverride_IsTheOverridesValue()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        await AddOverrideAsync(tenant, "members.max", EntitlementValue.Unlimited, expiresAt: null);

        var found = await FindAsync(tenant);

        found.Limits["members.max"].ShouldBeNull();
    }

    [Fact]
    public async Task FindEntitlements_AnOverrideThatExpired_IsThePlansValue()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        await AddOverrideAsync(tenant, "roles.custom", EntitlementValue.On, expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1));

        var found = await FindAsync(tenant);

        found.Features["roles.custom"].ShouldBeFalse();
    }

    [Fact]
    public async Task CheckFeature_Off_IsForbiddenWithTheKey()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        await using var scope = database.Services.CreateAsyncScope();

        var feature = await scope.ServiceProvider.GetRequiredService<IEntitlementDirectory>().CheckFeatureAsync(tenant.Id, Features.RolesCustom, Cancellation);

        feature.Error.Entitlement.ShouldBe("roles.custom");
    }

    // X3, E6: a module checks its limit with the effective value that the database holds: here the override's, over the plan's.
    [Fact]
    public async Task ReadLimit_AnOverride_IsTheOverridesNumber()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        await AddOverrideAsync(tenant, "members.max", EntitlementValue.Limit(9), expiresAt: null);
        await using var scope = database.Services.CreateAsyncScope();

        var read = await scope.ServiceProvider.GetRequiredService<IEntitlementDirectory>().ReadLimitAsync(tenant.Id, Limits.MembersMax, Cancellation);

        read.ShouldBe(9);
    }

    // X3, E6: FindAsync answers from the cache, so a write that the cache has not seen yet shows only in ReadLimitAsync.
    [Fact]
    public async Task ReadLimit_AChangeTheCacheHasNotSeen_IsTheDatabasesValue()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        await AddOverrideAsync(tenant, "members.max", EntitlementValue.Unlimited, expiresAt: null);
        await using var scope = database.Services.CreateAsyncScope();
        var controlPlane = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
        var directory = new EntitlementDirectory(
            controlPlane,
            new ReadCache(new FakeHybridCache(), NullLogger<ReadCache>.Instance, scope.ServiceProvider.GetRequiredService<IMeterFactory>()),
            TimeProvider.System);
        await directory.FindAsync(tenant.Id, Cancellation);
        await controlPlane.Database.ExecuteSqlAsync(
            $"UPDATE control_plane.entitlement_overrides SET maximum = 9, unlimited = false WHERE tenant_id = {tenant.Id} AND key = 'members.max'",
            Cancellation);

        var read = await directory.ReadLimitAsync(tenant.Id, Limits.MembersMax, Cancellation);

        (read, (await directory.FindAsync(tenant.Id, Cancellation)).Limits["members.max"]).ShouldBe(((int?)9, (int?)null));
    }

    [Fact]
    public async Task ReadLimit_AnUnlimitedOverride_IsNull()
    {
        var tenant = await TestData.AddTenantAsync(database.Services);
        await AddOverrideAsync(tenant, "members.max", EntitlementValue.Unlimited, expiresAt: null);
        await using var scope = database.Services.CreateAsyncScope();

        var read = await scope.ServiceProvider.GetRequiredService<IEntitlementDirectory>().ReadLimitAsync(tenant.Id, Limits.MembersMax, Cancellation);

        read.ShouldBeNull();
    }

    private async Task<TenantEntitlements> FindAsync(Tenant tenant)
    {
        await using var scope = database.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEntitlementDirectory>().FindAsync(tenant.Id, Cancellation);
    }

    private async Task AddOverrideAsync(Tenant tenant, string key, EntitlementValue value, DateTimeOffset? expiresAt)
    {
        var admin = await TestData.AddUserAsync(database.Services);
        await using var scope = database.Services.CreateAsyncScope();
        var controlPlane = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
        controlPlane.EntitlementOverrides.Add(new EntitlementOverride(tenant.Id, key, value, expiresAt, "A trial", admin.Id, DateTimeOffset.UtcNow));
        await controlPlane.SaveChangesAsync(Cancellation);
    }
}
