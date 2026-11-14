using ControlPlane.Application.FeatureFlags;
using ControlPlane.Application.FeatureFlags.SyncFeatureFlags;
using ControlPlane.Contracts.FeatureFlags;
using ControlPlane.Domain.FeatureFlags;
using ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace ControlPlane.IntegrationTests.FeatureFlags;

// The worker's sync compares the code's flags with the stored ones (docs/flows/feature-flags.md). Each test uses keys of its own, and
// the sync touches only the keys its catalog names, so the tests share the database.
public sealed class SyncFeatureFlagsTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SyncFeatureFlags_ANewFlag_StoresItWithTheCommitAndAnnouncesIt()
    {
        var key = Key();

        var messages = await SyncAsync(new FeatureFlagCatalog([Release(key)], []), "7c41d09");

        var flag = await FlagAsync(key);
        (flag.AddedRevision, flag.EveryoneElse, flag.DefaultValue).ShouldBe(("7c41d09", false, false));
        messages.OfType<FeatureFlagAddedToCodeEvent>().Single().ShouldBe(new FeatureFlagAddedToCodeEvent(
            messages.OfType<FeatureFlagAddedToCodeEvent>().Single().EventId, flag.AddedAt, flag.Id, key, "7c41d09", false));
        (await HistoryAsync(flag.Id)).ShouldBe([(FeatureFlagChangeKind.AddedToCode, "7c41d09")]);
    }

    [Fact]
    public async Task SyncFeatureFlags_TwiceWithTheSameCode_StoresAndAnnouncesOnce()
    {
        var catalog = new FeatureFlagCatalog([Release(Key())], []);
        await SyncAsync(catalog, "first00");

        var second = await SyncAsync(catalog, "second0");

        second.ShouldBeEmpty();
        (await FlagAsync(catalog.Flags[0].Key)).AddedRevision.ShouldBe("first00");
    }

    [Fact]
    public async Task SyncFeatureFlags_ARetiredFlag_ArchivesItWithTheCommitAndKeepsItsRules()
    {
        var key = Key();
        await SyncAsync(new FeatureFlagCatalog([Release(key)], []), "first00");

        var messages = await SyncAsync(new FeatureFlagCatalog([], [key]), "second0");

        var flag = await FlagAsync(key);
        (flag.RemovedRevision, flag.RemovedAt is not null, messages.OfType<FeatureFlagRemovedFromCodeEvent>().Single().Revision)
            .ShouldBe(("second0", true, "second0"));
    }

    [Fact]
    public async Task SyncFeatureFlags_AStoredFlagTheCodeDoesNotKnow_LeavesItAsItIs()
    {
        var key = Key();
        await SyncAsync(new FeatureFlagCatalog([Release(key)], []), "newer00");

        var messages = await SyncAsync(new FeatureFlagCatalog([], []), "older00");

        (messages.Count, (await FlagAsync(key)).RemovedAt).ShouldBe((0, null));
    }

    [Fact]
    public async Task SyncFeatureFlags_ANewDescription_TakesItAndAnnouncesNothing()
    {
        var key = Key();
        await SyncAsync(new FeatureFlagCatalog([Release(key)], []), null);

        var messages = await SyncAsync(new FeatureFlagCatalog([new FeatureFlagDefinition(key, FeatureFlagType.Release, "Edits many roles at once.")], []), null);

        (messages.Count, (await FlagAsync(key)).Description).ShouldBe((0, "Edits many roles at once."));
    }

    [Fact]
    public async Task SyncFeatureFlags_ACatalogThatIsNotSound_StoresNothing()
    {
        var key = Key();

        await Should.ThrowAsync<InvalidOperationException>(() => SyncAsync(new FeatureFlagCatalog([Release(key), Release(key)], []), null));

        (await InTenant.ReadAsync(database.Services, null, scope =>
            scope.GetRequiredService<ControlPlaneDbContext>().FeatureFlags.AnyAsync(flag => flag.Key == key, Cancellation))).ShouldBeFalse();
    }

    private static string Key() => $"test.flag_{Guid.NewGuid():N}";

    private static FeatureFlagDefinition Release(string key) => new(key, FeatureFlagType.Release, "Edits several roles at once.");

    private Task<OutgoingMessages> SyncAsync(FeatureFlagCatalog catalog, string? revision) =>
        InTenant.RunAsync(database.Services, null, scope => SyncFeatureFlagsHandler.HandleAsync(
            new SyncFeatureFlagsCommand(),
            scope.GetRequiredService<IFeatureFlagRepository>(),
            new FeatureFlagCode(catalog),
            new FeatureFlagOptions { Revision = revision },
            TimeProvider.System,
            Cancellation));

    private Task<FeatureFlag> FlagAsync(string key) =>
        InTenant.ReadAsync(database.Services, null, scope =>
            scope.GetRequiredService<ControlPlaneDbContext>().FeatureFlags.AsNoTracking().SingleAsync(flag => flag.Key == key, Cancellation));

    private async Task<List<(FeatureFlagChangeKind Kind, string? Revision)>> HistoryAsync(Guid flagId) =>
        [.. (await InTenant.ReadAsync(database.Services, null, scope =>
            scope.GetRequiredService<ControlPlaneDbContext>().FeatureFlagChanges.Where(change => change.FlagId == flagId).ToListAsync(Cancellation)))
            .Select(change => (change.Kind, change.Revision))];
}
