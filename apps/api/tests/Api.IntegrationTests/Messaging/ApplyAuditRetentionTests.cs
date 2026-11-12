using System.Globalization;
using Audit.Application.AuditLogs.ApplyAuditRetention;
using Audit.Application.AuditLogs.ApplyTenantAuditRetention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;

namespace Api.IntegrationTests.Messaging;

// K14, K15: the daily job deletes each tenant's entries outside its Activity log period, after the hold of 30 days that a shorter period
// starts, and the platform's entries older than Audit:Retention:Days. A missing value and unlimited keep every entry. The job runs here
// from the test, at the moment of a fake clock that starts now; the host does not start it by itself. The functions refuse a moment of
// the last 30 days by the database's clock, so the fake clock moves forward only past a hold. The tests run on a database of their own,
// because the job deletes the platform's old entries and reads every tenant (section 9, rule 10). A tenant's row is
// "days|held_from|held_until", with "-" for an empty value, to the minute.
public sealed class ApplyAuditRetentionTests(Database database) : IAsyncLifetime
{
    private const string Key = "audit.retention.days";

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private string _databaseName = null!;

    private TestData _testData = null!;

    private ApiFactory _api = null!;

    private DateTimeOffset Now => _clock.GetUtcNow();

    public async ValueTask InitializeAsync()
    {
        _databaseName = await database.CreateMigratedDatabaseAsync();
        _testData = new TestData(database, _databaseName);
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application, _databaseName),
            time: _clock,
            settings: new Dictionary<string, string?> { ["Audit:Retention:Days"] = "100" },
            configureServices: services =>
            {
                foreach (var job in services.Where(service => service.ImplementationType?.Name == "AuditRetentionJob").ToList())
                {
                    services.Remove(job);
                }
            });
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // The first run finds the shortening from no limit, the period before step 37c, to the plan's 365 days, and holds every entry.
    [Fact]
    public async Task ApplyAuditRetention_TheTenantsFirstRun_HoldsEveryEntryFor30Days()
    {
        var tenant = await _testData.AddTenantAsync();
        await AddEntriesAsync(tenant.Id, 1, days: 400);
        await AddEntriesAsync(tenant.Id, 1, days: 10);

        await ApplyAsync();

        (await EntriesAsync(tenant.Id)).ShouldBe(2);
        (await RowAsync(tenant.Id)).ShouldBe($"365|-|{Text(Now.AddDays(30))}");
    }

    [Fact]
    public async Task ApplyAuditRetention_AfterTheHold_DeletesEveryEntryOutsideThePeriodAndKeepsTheOthers()
    {
        var tenant = await _testData.AddTenantAsync();
        await AddRowAsync(tenant.Id, "365", "NULL", $"'{Now.AddMinutes(-1):O}'");
        await AddEntriesAsync(tenant.Id, 1001, days: 400);
        await AddEntriesAsync(tenant.Id, 1, days: 10);

        await ApplyAsync();

        (await EntriesAsync(tenant.Id)).ShouldBe(1);
        (await RowAsync(tenant.Id)).ShouldBe("365|-|-");
    }

    // E3: a plan without a value keeps every entry, as unlimited does.
    [Fact]
    public async Task ApplyAuditRetention_APlanWithoutAValue_DeletesNothing()
    {
        var tenant = await _testData.AddTenantAsync(planId: await _testData.AddPlanAsync(new()));
        await AddEntriesAsync(tenant.Id, 1, days: 4000);

        await ApplyAsync();

        (await EntriesAsync(tenant.Id)).ShouldBe(1);
        (await RowAsync(tenant.Id)).ShouldBe("-|-|-");
    }

    // R4: each tenant's deletion runs in its own transaction, which declares that tenant only.
    [Fact]
    public async Task ApplyAuditRetention_TwoTenants_DeletesEachOnesEntriesByItsOwnPeriod()
    {
        var limited = await _testData.AddTenantAsync();
        var unlimited = await _testData.AddTenantAsync(planId: await _testData.AddPlanAsync(new() { [Key] = TestData.Unlimited }));
        await AddRowAsync(limited.Id, "365", "NULL", "NULL");
        await AddEntriesAsync(limited.Id, 1, days: 400);
        await AddEntriesAsync(unlimited.Id, 1, days: 400);

        await ApplyAsync();

        (await EntriesAsync(limited.Id)).ShouldBe(0);
        (await EntriesAsync(unlimited.Id)).ShouldBe(1);
    }

    // The plan edit: a shorter value of the plan has no event for the tenant, so the job starts the hold at its own moment. The entries
    // that 365 days kept stay until the hold ends; older ones go at once.
    [Fact]
    public async Task ApplyAuditRetention_APlanEditedShorter_HoldsWhatThePeriodBeforeKeptUntilTheHoldEnds()
    {
        var plan = await _testData.AddPlanAsync(new() { [Key] = 365 });
        var tenant = await _testData.AddTenantAsync(planId: plan);
        await AddRowAsync(tenant.Id, "365", "NULL", "NULL");
        await _testData.SetPlanValueAsync(plan, Key, 90);
        await AddEntriesAsync(tenant.Id, 1, days: 400);
        await AddEntriesAsync(tenant.Id, 1, days: 200);
        await AddEntriesAsync(tenant.Id, 1, days: 10);
        var heldFrom = Text(Now.AddDays(-365));
        var heldUntil = Text(Now.AddDays(30));

        await ApplyAsync();
        var duringTheHold = await EntriesAsync(tenant.Id);
        var rowDuringTheHold = await RowAsync(tenant.Id);
        _clock.Advance(TimeSpan.FromDays(31));
        await ApplyAsync();

        (duringTheHold, rowDuringTheHold).ShouldBe((2, $"90|{heldFrom}|{heldUntil}"));
        (await EntriesAsync(tenant.Id)).ShouldBe(1);
        (await RowAsync(tenant.Id)).ShouldBe("90|-|-");
    }

    // The expiry: an override has no event when it expires (E3), so the job starts the hold at its own moment.
    [Fact]
    public async Task ApplyAuditRetention_AnOverrideThatExpired_HoldsWhatTheOverrideKept()
    {
        var plan = await _testData.AddPlanAsync(new() { [Key] = 365 });
        var tenant = await _testData.AddTenantAsync(planId: plan);
        await AddRowAsync(tenant.Id, "730", "NULL", "NULL");
        await AddOverrideAsync(tenant.Id, maximum: 730, expiresAt: Now.AddDays(-1));
        await AddEntriesAsync(tenant.Id, 1, days: 800);
        await AddEntriesAsync(tenant.Id, 1, days: 500);

        await ApplyAsync();

        (await EntriesAsync(tenant.Id)).ShouldBe(1);
        (await RowAsync(tenant.Id)).ShouldBe($"365|{Text(Now.AddDays(-730))}|{Text(Now.AddDays(30))}");
    }

    // A longer period applies at once, with no hold.
    [Fact]
    public async Task ApplyAuditRetention_ALongerPeriod_SetsItAndKeepsTheEntriesItKeeps()
    {
        var tenant = await _testData.AddTenantAsync();
        await AddRowAsync(tenant.Id, "90", "NULL", "NULL");
        await AddEntriesAsync(tenant.Id, 1, days: 200);

        await ApplyAsync();

        (await EntriesAsync(tenant.Id)).ShouldBe(1);
        (await RowAsync(tenant.Id)).ShouldBe("365|-|-");
    }

    // A purging tenant's log goes with the purge (R14), so the job leaves the tenant out and writes no row for it.
    [Fact]
    public async Task ApplyAuditRetention_APurgingTenant_WritesNoRow()
    {
        var tenant = await _testData.AddTenantAsync(status: "Purging");

        await ApplyAsync();

        (await RowAsync(tenant.Id)).ShouldBeNull();
    }

    // A tenant's message that arrives after the tenant's purge writes the row no more.
    [Fact]
    public async Task ApplyTenantAuditRetention_ATenantPurgedSinceTheList_WritesNoRow()
    {
        var tenant = await _testData.AddTenantRowAsync(status: "Purged");

        await _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeForTenantAsync(tenant.Id.ToString(), new ApplyTenantAuditRetentionCommand(tenant.Id), TestContext.Current.CancellationToken);
            return 0;
        });

        (await RowAsync(tenant.Id)).ShouldBeNull();
    }

    // K15: the platform's log follows Audit:Retention:Days, 100 days here, in batches of one transaction each.
    [Fact]
    public async Task ApplyAuditRetention_ThePlatformsLog_DeletesTheEntriesOlderThanItsSettingAndKeepsTheOthers()
    {
        await AddPlatformEntriesAsync(1001, days: 101);
        await AddPlatformEntriesAsync(2, days: 99);

        await ApplyAsync();

        (await database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM audit.platform_entries", _databaseName)).ShouldBe(2);
    }

    private static string Text(DateTimeOffset moment) => moment.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private Task ApplyAsync() =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new ApplyAuditRetentionCommand(), TestContext.Current.CancellationToken);
            return 0;
        });

    // Entries of the given age by the fake clock, written as the superuser, whom no policy binds.
    private Task<object?> AddEntriesAsync(Guid tenantId, int count, int days) =>
        database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_kind, kind, operation, target_kind, target_id)
            SELECT gen_random_uuid(), '{tenantId}', '{Now.AddDays(-days):O}'::timestamptz, 'Platform', 'Command', 'tenant.suspended', 'Tenant', '{tenantId}'
            FROM generate_series(1, {count})
            """,
            _databaseName);

    private Task<object?> AddPlatformEntriesAsync(int count, int days) =>
        database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.platform_entries (id, occurred_at, actor_kind, actor_id, kind, operation, target_kind, target_id)
            SELECT gen_random_uuid(), '{Now.AddDays(-days):O}'::timestamptz, 'User', gen_random_uuid(), 'Command', 'user.suspended', 'User', gen_random_uuid()
            FROM generate_series(1, {count})
            """,
            _databaseName);

    private Task<object?> AddRowAsync(Guid tenantId, string days, string heldFrom, string heldUntil) =>
        database.ScalarAsSuperuserAsync<object>(
            $"INSERT INTO audit.tenant_retentions (tenant_id, days, held_from, held_until) VALUES ('{tenantId}', {days}, {heldFrom}, {heldUntil})",
            _databaseName);

    // An override that a system admin set before, written as the superuser, because an override set now cannot expire in the past.
    private async Task AddOverrideAsync(Guid tenantId, int maximum, DateTimeOffset expiresAt)
    {
        var admin = await _testData.UserIdOfAsync(await _testData.AddSystemAdminAsync());
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO control_plane.entitlement_overrides (tenant_id, key, enabled, maximum, unlimited, expires_at, reason, set_by, set_at)
            VALUES ('{tenantId}', '{Key}', NULL, {maximum}, false, '{expiresAt:O}', 'A trial', '{admin}', '{Now.AddDays(-60):O}')
            """,
            _databaseName);
    }

    private async Task<long> EntriesAsync(Guid tenantId) =>
        await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}'", _databaseName);

    private Task<string?> RowAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT concat_ws('|', coalesce(days::text, '-'), coalesce(to_char(held_from AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI'), '-'),
                coalesce(to_char(held_until AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI'), '-'))
            FROM audit.tenant_retentions WHERE tenant_id = '{tenantId}'
            """,
            _databaseName);
}
