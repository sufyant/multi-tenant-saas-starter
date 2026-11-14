using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Platform.TermsVersions;

// POST /v1/platform/terms/versions: a system admin creates a terms version, a day of today or later, with an https link. One version may be
// scheduled at a time. A version that requires no acceptance tells every user who may accept by email.
public sealed class CreateTermsVersionTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateTermsVersion_ADayToCome_AnswersCreatedWithItsAddressAndScheduled()
    {
        await using var own = await OwnTerms.CreateAsync(database);

        var created = await own.Admin.CreateTermsVersionAsync(TermsRequests.Today.AddDays(10), summary: "  Shorter notice.  ");

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var version = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        created.Headers.Location!.ToString().ShouldBe($"/v1/platform/terms/versions/{version.GetProperty("id").GetGuid()}");
        (version.GetProperty("state").GetString(), version.GetProperty("summary").GetString(), version.GetProperty("acceptedCount").GetInt32(), version.GetProperty("locked").GetBoolean())
            .ShouldBe(("scheduled", "Shorter notice.", 0, false));
    }

    [Fact]
    public async Task CreateTermsVersion_ADayInThePast_IsInvalid()
    {
        await using var own = await OwnTerms.CreateAsync(database);

        var created = await own.Admin.CreateTermsVersionAsync(TermsRequests.Today.AddDays(-1));

        (created.StatusCode, await TermsRequests.CodeOfAsync(created)).ShouldBe((HttpStatusCode.BadRequest, "terms_version.effective_date_past"));
    }

    [Fact]
    public async Task CreateTermsVersion_AnHttpLink_IsInvalid()
    {
        await using var own = await OwnTerms.CreateAsync(database);

        var created = await own.Admin.CreateTermsVersionAsync(TermsRequests.Today, "http://brand.example/terms");

        (created.StatusCode, await TermsRequests.CodeOfAsync(created)).ShouldBe((HttpStatusCode.BadRequest, "terms_version.url_invalid"));
    }

    [Fact]
    public async Task CreateTermsVersion_ALinkOverTheLimit_IsAValidationFailure()
    {
        await using var own = await OwnTerms.CreateAsync(database);

        var created = await own.Admin.CreateTermsVersionAsync(TermsRequests.Today, $"https://brand.example/{new string('a', 2048)}");

        (created.StatusCode, await TermsRequests.CodeOfAsync(created)).ShouldBe((HttpStatusCode.BadRequest, "request.validation_failed"));
    }

    [Fact]
    public async Task CreateTermsVersion_AnotherScheduled_IsAConflictAndAddsNothing()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));

        var created = await own.Admin.CreateTermsVersionAsync(TermsRequests.Today.AddDays(20));

        (created.StatusCode, await TermsRequests.CodeOfAsync(created)).ShouldBe((HttpStatusCode.Conflict, "terms_version.scheduled_exists"));
        (await own.ScalarAsync<long>("SELECT count(*) FROM control_plane.terms_versions")).ShouldBe(1);
    }

    // A version of today is in force at once, so another version may follow it.
    [Fact]
    public async Task CreateTermsVersion_AVersionOfToday_IsInForceAtOnce()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        await own.Admin.AddTermsVersionAsync(TermsRequests.Today);

        var created = await own.Admin.CreateTermsVersionAsync(TermsRequests.Today.AddDays(10));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await own.Admin.TermsOfUseAsync()).GetProperty("versions").EnumerateArray().Select(version => version.GetProperty("state").GetString())
            .ShouldBe(["scheduled", "current"]);
    }

    // K3: a version belongs to no tenant, so its creation goes to the platform's log only, by the system admin, with its values.
    [Fact]
    public async Task CreateTermsVersion_ANewVersion_RecordsItInThePlatformLog()
    {
        await using var own = await OwnTerms.CreateAsync(database);

        var versionId = await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), summary: "New"));

        var adminId = await own.Data.UserIdOfAsync(own.AdminId);
        (await own.ScalarAsync<string>($"SELECT concat_ws('|', operation, actor_kind, actor_id, target_kind, coalesce(target_name, '-')) FROM audit.platform_entries WHERE target_id = '{versionId}'"))
            .ShouldBe($"terms_version.created|User|{adminId}|TermsVersion|-");
        (await own.ScalarAsync<string>($"SELECT details::text FROM audit.platform_entries WHERE target_id = '{versionId}'"))
            .ShouldBe($$"""{"url": "{{TermsRequests.Link}}", "summary": "New", "effectiveDate": "{{TermsRequests.Today.AddDays(10):yyyy-MM-dd}}", "requiresAcceptance": true}""");
    }

    // A version that requires no acceptance tells every user who may accept: a tenant user who is active. A suspended user, a person who
    // joined no tenant and the system admin who created it are told nothing.
    [Fact]
    public async Task CreateTermsVersion_RequiringNoAcceptance_EmailsEveryActiveTenantUserOnly()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var told = TestEmails.Of($"{Guid.NewGuid():N}@example.com");
        await own.AddTenantUserAsync(tenantId, told);
        var (suspended, _) = await own.AddTenantUserAsync(tenantId);
        await own.Data.SuspendUserAsync(suspended);
        await own.Data.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        await own.Data.AddMemberAsync(tenantId, own.AdminId);

        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), summary: "Shorter notice.", requiresAcceptance: false));

        own.Api.Email.Sent.Select(email => email.To).ShouldBe([told]);
        own.Api.Email.Sent.Single().Subject.ShouldStartWith("Our terms of use change on");
    }

    [Fact]
    public async Task CreateTermsVersion_RequiringAcceptance_EmailsNobody()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);

        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10)));

        own.Api.Email.Sent.ShouldBeEmpty();
    }
}
