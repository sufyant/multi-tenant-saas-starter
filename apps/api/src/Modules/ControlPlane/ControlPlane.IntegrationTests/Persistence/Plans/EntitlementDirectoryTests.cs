using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Tenants;
using ControlPlane.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

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
        found.Limits.ShouldBe(new Dictionary<string, int?> { ["members.max"] = 5, ["roles.custom.max"] = 0 }, ignoreOrder: true);
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
