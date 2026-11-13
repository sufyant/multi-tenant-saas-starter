using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Tenant.AuditLogs;

// GET /v1/tenants/{tenantId}/audit-log/retention: the tenant's Activity log period and, while a hold runs and an entry that the hold keeps
// is outside the new period, the new period and the moment those entries go (K14). It reads what the daily job would do at this moment,
// from the same state, and writes nothing. The tests run on a database of their own, at the moment of a fake clock, with no job: the job
// of another test's host would write the rows these tests set (section 9, rule 10). An answer is "days|shortening's days|goesAt", with
// "-" for an empty value, to the minute.
public sealed class GetAuditRetentionTests(Database database) : IAsyncLifetime
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private string _databaseName = null!;

    private TestData _testData = null!;

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private DateTimeOffset Now => _clock.GetUtcNow();

    public async ValueTask InitializeAsync()
    {
        _databaseName = await database.CreateMigratedDatabaseAsync();
        _testData = new TestData(database, _databaseName);
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application, _databaseName),
            time: _clock,
            configureServices: services =>
            {
                foreach (var job in services.Where(service => service.ImplementationType?.Name == "AuditRetentionJob").ToList())
                {
                    services.Remove(job);
                }
            });
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // E3: unlimited keeps every entry, so nothing goes.
    [Fact]
    public async Task GetAuditRetention_NoLimit_AnswersNoPeriodAndNoShortening()
    {
        var tenant = await TenantAsync(days: 0);
        await AddEntryAsync(tenant, days: 4000);

        (await RetentionAsync(tenant)).ShouldBe("-|-|-");
    }

    [Fact]
    public async Task GetAuditRetention_APeriodWithNoHold_AnswersThePeriodAndNoShortening()
    {
        var tenant = await TenantAsync(days: 365);
        await AddRowAsync(tenant, "365", "NULL", "NULL");
        await AddEntryAsync(tenant, days: 10);

        (await RetentionAsync(tenant)).ShouldBe("365|-|-");
    }

    // The Banner of 12.10: a hold runs, and an entry it keeps is older than the new period, so that entry goes when the hold ends.
    [Fact]
    public async Task GetAuditRetention_AHoldAndAnEntryOlderThanTheNewPeriod_AnswersTheNewPeriodAndTheHoldsEnd()
    {
        var tenant = await TenantAsync(days: 90);
        await AddRowAsync(tenant, "90", "NULL", $"'{Now.AddDays(20):O}'");
        await AddEntryAsync(tenant, days: 200);

        (await RetentionAsync(tenant)).ShouldBe($"90|90|{Text(Now.AddDays(20))}");
    }

    // No entry goes, so the Activity log shows no Banner.
    [Fact]
    public async Task GetAuditRetention_AHoldAndNoEntryOlderThanTheNewPeriod_AnswersNoShortening()
    {
        var tenant = await TenantAsync(days: 90);
        await AddRowAsync(tenant, "90", "NULL", $"'{Now.AddDays(20):O}'");
        await AddEntryAsync(tenant, days: 10);

        (await RetentionAsync(tenant)).ShouldBe("90|-|-");
    }

    // A hold keeps the entries from its start on. An entry older than both the start and the new period goes at the job's next run with
    // or without the hold, so it is not what the hold keeps.
    [Theory]
    [InlineData(200, "90|90|")]
    [InlineData(400, "90|-|-")]
    public async Task GetAuditRetention_AHoldFromAMoment_CountsOnlyTheEntriesItKeeps(int age, string answer)
    {
        var tenant = await TenantAsync(days: 90);
        await AddRowAsync(tenant, "90", $"'{Now.AddDays(-365):O}'", $"'{Now.AddDays(20):O}'");
        await AddEntryAsync(tenant, days: age);

        (await RetentionAsync(tenant)).ShouldStartWith(answer);
    }

    // The job clears a hold that has ended, and deletes what it held at its next run.
    [Fact]
    public async Task GetAuditRetention_AfterTheHoldEnds_AnswersNoShortening()
    {
        var tenant = await TenantAsync(days: 90);
        await AddRowAsync(tenant, "90", "NULL", $"'{Now.AddMinutes(-1):O}'");
        await AddEntryAsync(tenant, days: 200);

        (await RetentionAsync(tenant)).ShouldBe("90|-|-");
    }

    // A plan edit has no event for the tenant: the job starts the hold at its next run. Until then the answer is the hold that the job
    // would start at this moment, so the date shown is never earlier than the job's.
    [Fact]
    public async Task GetAuditRetention_APlanEditedShorterBeforeTheJobsRun_AnswersTheHoldTheJobWouldStart()
    {
        var tenant = await TenantAsync(days: 90);
        await AddRowAsync(tenant, "365", "NULL", "NULL");
        await AddEntryAsync(tenant, days: 200);

        (await RetentionAsync(tenant)).ShouldBe($"90|90|{Text(Now.AddDays(30))}");
    }

    // A tenant the job has not seen yet has no row: its first run holds every entry for 30 days. The read writes no row.
    [Fact]
    public async Task GetAuditRetention_ATenantWithNoRowYet_AnswersTheFirstRunsHoldAndWritesNoRow()
    {
        var tenant = await TenantAsync(days: 365);
        await AddEntryAsync(tenant, days: 400);

        (await RetentionAsync(tenant)).ShouldBe($"365|365|{Text(Now.AddDays(30))}");
        (await RowCountAsync(tenant)).ShouldBe(0);
    }

    [Fact]
    public async Task GetAuditRetention_AsAnAdmin_IsAllowed()
    {
        var tenant = await TenantAsync(days: 365);
        var admin = await _testData.AddMemberAsync(tenant, role: "Admin");

        var response = await _api.CreateClient(admin).GetAsync($"/v1/tenants/{tenant}/audit-log/retention", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAuditRetention_AsAMember_IsForbidden()
    {
        var tenant = await TenantAsync(days: 365);
        var member = await _testData.AddMemberAsync(tenant, role: "Member");

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant}/audit-log/retention", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // R1: row level security keeps the row and the entries to the tenant of the path. Another tenant's hold and old entry never count.
    [Fact]
    public async Task GetAuditRetention_AnotherTenantsHoldAndEntries_NeverCount()
    {
        var tenant = await TenantAsync(days: 90);
        var other = await TenantAsync(days: 90);
        await AddRowAsync(tenant, "90", "NULL", "NULL");
        await AddEntryAsync(tenant, days: 10);
        await AddRowAsync(other, "90", "NULL", $"'{Now.AddDays(20):O}'");
        await AddEntryAsync(other, days: 200);

        (await RetentionAsync(tenant)).ShouldBe("90|-|-");
    }

    private static string Text(DateTimeOffset moment) => moment.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string DaysOf(JsonElement days) => days.ValueKind == JsonValueKind.Null ? "-" : $"{days.GetInt32()}";

    // The answer an Owner of the tenant reads, written as one text.
    private async Task<string> RetentionAsync(Guid tenantId)
    {
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenantId, role: "Owner"));
        var response = await owner.GetAsync($"/v1/tenants/{tenantId}/audit-log/retention", Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        answer.EnumerateObject().Select(field => field.Name).ShouldBe(["days", "shortening"], ignoreOrder: true);
        var shortening = answer.GetProperty("shortening");
        return shortening.ValueKind == JsonValueKind.Null
            ? $"{DaysOf(answer.GetProperty("days"))}|-|-"
            : $"{DaysOf(answer.GetProperty("days"))}|{shortening.GetProperty("days").GetInt32()}|{Text(shortening.GetProperty("goesAt").GetDateTimeOffset())}";
    }

    // A tenant on a plan of its own with this Activity log period; 0 is unlimited.
    private async Task<Guid> TenantAsync(int days) =>
        (await _testData.AddTenantAsync(planId: await PlanAnswers.RetentionPlanAsync(_testData, days))).Id;

    // An entry of the given age by the fake clock, written as the superuser, whom no policy binds.
    private Task<object?> AddEntryAsync(Guid tenantId, int days) =>
        database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_kind, kind, operation, target_kind, target_id)
            VALUES (gen_random_uuid(), '{tenantId}', '{Now.AddDays(-days):O}'::timestamptz, 'Platform', 'Command', 'tenant.suspended', 'Tenant', '{tenantId}')
            """,
            _databaseName);

    private Task<object?> AddRowAsync(Guid tenantId, string days, string heldFrom, string heldUntil) =>
        database.ScalarAsSuperuserAsync<object>(
            $"INSERT INTO audit.tenant_retentions (tenant_id, days, held_from, held_until) VALUES ('{tenantId}', {days}, {heldFrom}, {heldUntil})",
            _databaseName);

    private Task<long> RowCountAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.tenant_retentions WHERE tenant_id = '{tenantId}'", _databaseName);
}
