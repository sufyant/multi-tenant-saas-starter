using System.Net;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Caching;

// Section 3, "Cache", for the terms gate (A11): the gate reads the versions and the user's acceptances through Valkey. A version's
// creation, edit or cancellation, and the user's acceptance, remove the entry after the commit, so the next request answers at once. The
// day decides each version's state at each read, so a version's day comes with no write and no wait for the expiry. Each test reads
// first, so the old answer is in the cache, then changes something, then reads again. One scheduled version at a time is the rule of the
// whole database, so each test runs on a database of its own.
[FixedClock("Each test runs on a database of its own, so no host of another test runs what its clock schedules.")]
public sealed class TermsGateCacheTests(Valkey valkey, Database database) : IClassFixture<Valkey>
{
    private static readonly DateOnly Today = new(2026, 12, 1);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CheckTerms_AfterAVersionIsCreated_RefusesAtOnce()
    {
        await using var own = await CachedTerms.CreateAsync(database, valkey);
        var before = await own.MembersAsync();

        await own.Admin.AddTermsVersionAsync(Today.AddDays(10));
        var after = await own.MembersAsync();

        (before, after).ShouldBe((HttpStatusCode.OK, HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task CheckTerms_AfterTheUserAccepts_AnswersAtOnce()
    {
        await using var own = await CachedTerms.CreateAsync(database, valkey);
        var versionId = await own.Admin.AddTermsVersionAsync(Today.AddDays(10));
        var before = await own.MembersAsync();

        (await own.Member.AcceptTermsVersionAsync(versionId)).EnsureSuccessStatusCode();
        var after = await own.MembersAsync();

        (before, after).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    [Fact]
    public async Task CheckTerms_AfterTheScheduledVersionIsEditedToNeedNoAcceptance_AnswersAtOnce()
    {
        await using var own = await CachedTerms.CreateAsync(database, valkey);
        var versionId = await own.Admin.AddTermsVersionAsync(Today.AddDays(10));
        var before = await own.MembersAsync();

        (await own.Admin.UpdateTermsVersionAsync(versionId, Today.AddDays(10), requiresAcceptance: false)).EnsureSuccessStatusCode();
        var after = await own.MembersAsync();

        (before, after).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    [Fact]
    public async Task CheckTerms_AfterTheScheduledVersionIsCancelled_AnswersAtOnce()
    {
        await using var own = await CachedTerms.CreateAsync(database, valkey);
        var versionId = await own.Admin.AddTermsVersionAsync(Today.AddDays(10));
        var before = await own.MembersAsync();

        (await own.Admin.CancelTermsVersionAsync(versionId)).EnsureSuccessStatusCode();
        var after = await own.MembersAsync();

        (before, after).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    // The version in force asks for acceptance; tomorrow a version that asks for none replaces it. The entry the first request cached
    // still holds, as no write removed it, and the gate weighs it on the new day: the second request is answered.
    [Fact]
    public async Task CheckTerms_TheDayOfTheNextVersionComesWhileTheEntryIsCached_DecidesByTheNewDay()
    {
        await using var own = await CachedTerms.CreateAsync(database, valkey);
        await own.Admin.AddTermsVersionAsync(Today);
        await own.Admin.AddTermsVersionAsync(Today.AddDays(1), "https://brand.example/terms/next", requiresAcceptance: false);
        var before = await own.MembersAsync();

        own.Time.Advance(TimeSpan.FromDays(1));
        var after = await own.MembersAsync();

        (before, after).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    // A database of its own with a cached host on a fake clock, its system admin and one member of one tenant.
    [FixedClock("It runs on a database of its own, so no host of another test runs what its clock schedules.")]
    private sealed class CachedTerms : IAsyncDisposable
    {
        private CachedTerms(ApiFactory api, FakeTimeProvider time, HttpClient admin, HttpClient member, Guid tenantId)
        {
            Api = api;
            Time = time;
            Admin = admin;
            Member = member;
            TenantId = tenantId;
        }

        public ApiFactory Api { get; }

        public FakeTimeProvider Time { get; }

        public HttpClient Admin { get; }

        public HttpClient Member { get; }

        public Guid TenantId { get; }

        public static async Task<CachedTerms> CreateAsync(Database database, Valkey valkey)
        {
            var name = await database.CreateMigratedDatabaseAsync();
            var data = new TestData(database, name);
            var time = new FakeTimeProvider(new DateTimeOffset(Today, new TimeOnly(9, 0), TimeSpan.Zero));
            var api = new ApiFactory(
                database.ConnectionStringFor(DatabaseRoles.Application, name),
                time: time,
                configureServices: ApiFactory.RemoveAuditRetention,
                settings: new Dictionary<string, string?> { ["ConnectionStrings:Cache"] = valkey.ConnectionString });
            var (tenantId, _) = await data.AddTenantAsync();
            var member = await data.AddMemberAsync(tenantId);
            return new CachedTerms(api, time, api.CreateClient(await data.AddSystemAdminAsync(), secondFactor: true), api.CreateClient(member), tenantId);
        }

        public async Task<HttpStatusCode> MembersAsync() =>
            (await Member.GetAsync($"/v1/tenants/{TenantId}/members", Cancellation)).StatusCode;

        public async ValueTask DisposeAsync() => await Api.DisposeAsync();
    }
}
