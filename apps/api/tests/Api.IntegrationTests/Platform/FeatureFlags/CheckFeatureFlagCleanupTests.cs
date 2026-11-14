using ControlPlane.Application.FeatureFlags.CheckFeatureFlagCleanup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Platform.FeatureFlags;

// The worker's look at the flags' cleanup, at its start: every system admin hears, in the platform's bell, how many flags are ready to
// remove and how many are unused (3.07). The week's copies tell each of them once.
[FixedClock("Each test runs on a database of its own, so no host of another test runs what its clock schedules.")]
public sealed class CheckFeatureFlagCleanupTests(Database database)
{
    // A Monday: the ISO week 2026-W47 runs until Sunday 22 November.
    private static readonly DateTimeOffset Monday = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CheckFeatureFlagCleanup_AtTheWorkersStart_TellsEverySystemAdminTheCountsInThePlatformsBell()
    {
        await using var own = await OwnFlags.CreateAsync(database, AddFlagsAsync);

        await Waiting.UntilAsync(async () => await CountAsync(own) == 2);

        (await own.ScalarAsync<string>(
            "SELECT string_agg(type || ' ' || bell || ' ' || (values->>'count'), ', ' ORDER BY type) FROM notifications.notifications WHERE type LIKE 'feature_flags.%'"))
            .ShouldBe("feature_flags.ready_to_remove Platform 1, feature_flags.unused Platform 2");
    }

    // The worker asks every 24 hours. The asks of the start's week tell nobody again; the first ask of the next week tells again.
    [Fact]
    public async Task CheckFeatureFlagCleanup_EveryDayOfTwoWeeks_TellsOnceAWeek()
    {
        var time = new FakeTimeProvider(Monday);
        await using var own = await OwnFlags.CreateAsync(database, AddFlagsAsync, time);
        await Waiting.UntilAsync(async () => await CountAsync(own) == 2);

        await own.Api.TrackMessagesAsync(() => AdvanceAsync(time, TimeSpan.FromDays(1)), until: new CleanupChecked());
        var tuesday = await CountAsync(own);
        await own.Api.TrackMessagesAsync(() => AdvanceAsync(time, TimeSpan.FromDays(5)), until: new CleanupChecked());
        var sunday = await CountAsync(own);
        await own.Api.TrackMessagesAsync(() => AdvanceAsync(time, TimeSpan.FromDays(1)), until: new CleanupChecked());
        var nextMonday = await CountAsync(own);

        (tuesday, sunday, nextMonday).ShouldBe((2, 2, 4));
    }

    // A worker that starts again in the same week asks again at its start, as the job does, and tells nobody twice. The second host has no
    // job of its own, so the test sends the ask the job sends at the start.
    [Fact]
    public async Task CheckFeatureFlagCleanup_AWorkerStartsAgainInTheSameWeek_TellsNobodyTwice()
    {
        var time = new FakeTimeProvider(Monday);
        await using var own = await OwnFlags.CreateAsync(database, AddFlagsAsync, time);
        await Waiting.UntilAsync(async () => await CountAsync(own) == 2);
        await own.Api.DisposeAsync();
        time.Advance(TimeSpan.FromDays(3));
        await using var restarted = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application, own.Name),
            configureServices: services =>
            {
                ApiFactory.RemoveAuditRetention(services);
                ApiFactory.RemoveFeatureFlagJob(services);
            },
            time: time);

        await restarted.WaitingForMessagesAsync(async () =>
        {
            using var scope = restarted.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new CheckFeatureFlagCleanupCommand(), Cancellation);
            return 0;
        });

        (await CountAsync(own)).ShouldBe(2);
    }

    private static Task AdvanceAsync(FakeTimeProvider time, TimeSpan by)
    {
        time.Advance(by);
        return Task.CompletedTask;
    }

    private static async Task<long> CountAsync(OwnFlags own) =>
        await own.ScalarAsync<long>("SELECT count(*) FROM notifications.notifications WHERE type LIKE 'feature_flags.%'");

    private async Task AddFlagsAsync(string name)
    {
        await OwnFlags.AddFlagAsync(database, name, "roles.bulk_edit", everyoneElse: true, daysAgo: 40);
        await OwnFlags.AddFlagAsync(database, name, "reports.beta", daysAgo: 34);
        await OwnFlags.AddFlagAsync(database, name, "search.old", daysAgo: 31);
    }

    // The session ends once the job's ask has run and every message it caused has been handled.
    private sealed class CleanupChecked : ITrackedCondition
    {
        private readonly SettledMessages _settled = new();

        private bool _checked;

        public void Record(EnvelopeRecord record)
        {
            _settled.Record(record);
            _checked |= record.Message is CheckFeatureFlagCleanupCommand && record.MessageEventType == MessageEventType.MessageSucceeded;
        }

        public bool IsCompleted() => _checked && _settled.IsCompleted();
    }
}
