using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Notifications.Application.Notifications.DeliverNotification;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Platform.TermsVersions;

// POST /v1/platform/terms/versions: a system admin creates a terms version, a day of today or later, with an https link. One version may be
// scheduled at a time. A version that emails the users tells every user who may accept; a small change emails nobody.
public sealed class CreateTermsVersionTests(Database database)
{
    private const string AcceptAtSignIn = "You accept them the next time you sign in.";

    private const string NothingToDo = "You do not need to do anything.";

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
            .ShouldBe(["scheduled", "in_force"]);
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
            .ShouldBe($$"""{"url": "{{TermsRequests.Link}}", "summary": "New", "sendsEmail": true, "effectiveDate": "{{TermsRequests.Today.AddDays(10):yyyy-MM-dd}}", "requiresAcceptance": true}""");
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

    // A version that requires acceptance tells the person before their next sign-in, where the gate asks.
    [Fact]
    public async Task CreateTermsVersion_RequiringAcceptance_EmailsThatThePersonAcceptsAtSignIn()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);

        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10)));

        own.Api.Email.Sent.Single().Text.ShouldContain("You accept them the next time you sign in.");
    }

    // A small change, such as a typo, emails nobody (the maintainer's decision of 2026-11-14).
    [Fact]
    public async Task CreateTermsVersion_WithoutEmail_EmailsNobody()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);

        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), requiresAcceptance: false, sendsEmail: false));

        own.Api.Email.Sent.ShouldBeEmpty();
    }

    // Every active tenant user, once even when they are a member of two tenants. No suspended user, no deleted user, no user without a
    // membership, and not the system admin who created the version (U3).
    [Fact]
    public async Task CreateTermsVersion_UsersOfTwoTenants_EmailsEveryActiveTenantUserOnce()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (first, _) = await own.Data.AddTenantAsync();
        var (second, _) = await own.Data.AddTenantAsync();
        var both = TestEmails.Of($"{Guid.NewGuid():N}@example.com");
        var onlyFirst = TestEmails.Of($"{Guid.NewGuid():N}@example.com");
        var onlySecond = TestEmails.Of($"{Guid.NewGuid():N}@example.com");
        var (inBoth, _) = await own.AddTenantUserAsync(first, both);
        await own.Data.AddMemberAsync(second, inBoth);
        await own.AddTenantUserAsync(first, onlyFirst);
        await own.AddTenantUserAsync(second, onlySecond);
        var (suspended, _) = await own.AddTenantUserAsync(first);
        await own.Data.SuspendUserAsync(suspended);
        var (deleted, _) = await own.AddTenantUserAsync(second);
        await own.Data.DeleteUserAsync(deleted);
        await own.Data.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        await own.Data.AddMemberAsync(first, own.AdminId);

        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10)));

        own.Api.Email.Sent.Select(email => email.To).ShouldBe([both, onlyFirst, onlySecond], ignoreOrder: true);
    }

    // "Accept at your next sign-in" only when the version requires acceptance; else the person does nothing.
    [Fact]
    public async Task CreateTermsVersion_RequiringNoAcceptance_SaysThereIsNothingToDo()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);

        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), requiresAcceptance: false));

        var text = own.Api.Email.Sent.Single().Text;
        text.ShouldContain(NothingToDo);
        text.ShouldNotContain(AcceptAtSignIn);
    }

    // Three users at one delivery a second: the first goes at once, the next a second later, the last two seconds later.
    [Fact]
    public async Task CreateTermsVersion_ThreeUsersAtOneDeliveryASecond_SchedulesOneDeliveryEachSecond()
    {
        await using var own = await OwnTerms.CreateAsync(database, new Dictionary<string, string?> { ["Notifications:Pace:DeliveriesPerSecond"] = "1" });
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);
        await own.AddTenantUserAsync(tenantId);
        await own.AddTenantUserAsync(tenantId);

        var session = await own.Api.TrackMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10)));

        var later = session.Scheduled.Envelopes().Where(envelope => envelope.Message is DeliverNotificationCommand).Select(envelope => envelope.ScheduledTime!.Value).Order().ToList();
        var atOnce = session.Sent.MessagesOf<DeliverNotificationCommand>().Count() - later.Count;
        (atOnce, later.Count).ShouldBe((1, 2));
        Math.Round((later[1] - later[0]).TotalSeconds).ShouldBe(1);
    }
}
