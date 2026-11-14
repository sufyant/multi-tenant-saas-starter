using ControlPlane.Application.FeatureFlags;
using ControlPlane.Application.FeatureFlags.SyncFeatureFlags;
using ControlPlane.Contracts.FeatureFlags;
using ControlPlane.Domain.FeatureFlags;
using ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ControlPlane.IntegrationTests.FeatureFlags;

// The one evaluation (IFeatureFlags) reads the stored rules of the code's flags for one organisation, and answers a flag's default value
// when the environment does not store it yet (docs/flows/feature-flags.md).
public sealed class FeatureFlagEvaluationTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task IsOn_AFlagNotStoredYet_AnswersItsDefaultValue()
    {
        var release = new FeatureFlagDefinition(Key(), FeatureFlagType.Release, "New.");
        var killSwitch = new FeatureFlagDefinition(Key(), FeatureFlagType.KillSwitch, "Stops.");
        await using var services = Services(new FeatureFlagCatalog([release, killSwitch], []));

        var flags = await ListAsync(services, Guid.CreateVersion7());

        (flags[release.Key], flags[killSwitch.Key]).ShouldBe((false, true));
    }

    [Fact]
    public async Task IsOn_AListedOrganisation_IsOnAndAnotherIsOff()
    {
        var definition = new FeatureFlagDefinition(Key(), FeatureFlagType.Release, "New.");
        var catalog = new FeatureFlagCatalog([definition], []);
        await using var services = Services(catalog);
        await SyncAsync(services, catalog);
        var listed = await TestData.AddTenantAsync(services);
        var other = await TestData.AddTenantAsync(services);
        await ListAsync(services, definition.Key, listed.Id);

        var flags = await InTenant.ReadAsync(services, null, async scope =>
        {
            var evaluation = scope.GetRequiredService<IFeatureFlags>();
            return (await evaluation.IsOnAsync(definition.Key, listed.Id, Cancellation), await evaluation.IsOnAsync(definition.Key, other.Id, Cancellation));
        });

        flags.ShouldBe((true, false));
    }

    [Fact]
    public async Task IsOn_EveryoneElseOn_IsOnForEveryOrganisation()
    {
        var definition = new FeatureFlagDefinition(Key(), FeatureFlagType.Release, "New.");
        var catalog = new FeatureFlagCatalog([definition], []);
        await using var services = Services(catalog);
        await SyncAsync(services, catalog);
        await InTenant.RunAsync(services, null, async scope =>
            (await scope.GetRequiredService<IFeatureFlagRepository>().FindFlagForUpdateAsync(definition.Key, Cancellation))!
                .SetEveryoneElse(true, await AdminAsync(services), DateTimeOffset.UtcNow));

        (await ListAsync(services, Guid.CreateVersion7()))[definition.Key].ShouldBeTrue();
    }

    // Work of no organisation, such as an email, reads rule 3 alone: a listed organisation turns nothing on for it.
    [Fact]
    public async Task IsOnForEveryoneElse_AKillSwitchTurnedOffWithAListedOrganisation_IsOff()
    {
        var definition = new FeatureFlagDefinition(Key(), FeatureFlagType.KillSwitch, "Stops.");
        var catalog = new FeatureFlagCatalog([definition], []);
        await using var services = Services(catalog);
        await SyncAsync(services, catalog);
        var listed = await TestData.AddTenantAsync(services);
        await ListAsync(services, definition.Key, listed.Id);
        var admin = await AdminAsync(services);
        await InTenant.RunAsync(services, null, async scope =>
            (await scope.GetRequiredService<IFeatureFlagRepository>().FindFlagForUpdateAsync(definition.Key, Cancellation))!
                .SetEveryoneElse(false, admin, DateTimeOffset.UtcNow));

        (await InTenant.ReadAsync(services, null, scope => scope.GetRequiredService<IFeatureFlags>().IsOnForEveryoneElseAsync(definition.Key, Cancellation))).ShouldBeFalse();
    }

    [Fact]
    public async Task IsOnForEveryoneElse_AKillSwitchNotStoredYet_IsOn()
    {
        var definition = new FeatureFlagDefinition(Key(), FeatureFlagType.KillSwitch, "Stops.");
        await using var services = Services(new FeatureFlagCatalog([definition], []));

        (await InTenant.ReadAsync(services, null, scope => scope.GetRequiredService<IFeatureFlags>().IsOnForEveryoneElseAsync(definition.Key, Cancellation))).ShouldBeTrue();
    }

    [Fact]
    public async Task IsOn_AFlagTheCodeDoesNotDefine_IsAMistake()
    {
        await using var services = Services(new FeatureFlagCatalog([], []));

        await Should.ThrowAsync<ArgumentException>(() => InTenant.ReadAsync(services, null, scope =>
            scope.GetRequiredService<IFeatureFlags>().IsOnAsync("test.unknown", Guid.CreateVersion7(), Cancellation)));
    }

    private static string Key() => $"test.flag_{Guid.NewGuid():N}";

    private static Task<IReadOnlyDictionary<string, bool>> ListAsync(ServiceProvider services, Guid tenantId) =>
        InTenant.ReadAsync(services, null, scope => scope.GetRequiredService<IFeatureFlags>().ListAsync(tenantId, Cancellation));

    private static Task<Wolverine.OutgoingMessages> SyncAsync(IServiceProvider services, FeatureFlagCatalog catalog) =>
        InTenant.RunAsync(services, null, scope => SyncFeatureFlagsHandler.HandleAsync(
            new SyncFeatureFlagsCommand(),
            scope.GetRequiredService<IFeatureFlagRepository>(),
            new FeatureFlagCode(catalog),
            new FeatureFlagOptions(),
            TimeProvider.System,
            Cancellation));

    private static async Task<Guid> AdminAsync(IServiceProvider services) => (await TestData.AddUserAsync(services)).Id;

    private static async Task ListAsync(IServiceProvider services, string key, Guid tenantId)
    {
        var admin = await AdminAsync(services);
        await InTenant.RunAsync(services, null, async scope =>
        {
            var flags = scope.GetRequiredService<IFeatureFlagRepository>();
            var flag = (await flags.FindFlagForUpdateAsync(key, Cancellation))!;
            flags.Record(flag.AddTenant(tenantId, false, admin, DateTimeOffset.UtcNow).Value!);
            flags.List(new FeatureFlagTenant(flag.Id, tenantId, admin, DateTimeOffset.UtcNow));
        });
    }

    private ServiceProvider Services(FeatureFlagCatalog catalog) =>
        database.BuildServices(configure: services => services.Replace(ServiceDescriptor.Singleton(new FeatureFlagCode(catalog))));
}
