using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Me.TermsVersions;

// GET /v1/me/terms, and the person's answers: a version that requires acceptance asks for it, the version in force first, then the scheduled
// one; a version that requires no acceptance gives a notice until the person closes it.
public sealed class GetMyTermsTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetMyTerms_AVersionInForceAndAScheduledOneToAccept_AsksForEachInTurn()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var current = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);
        var scheduled = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), "https://brand.example/terms/next");

        var first = TermsRequests.AskOf(await person.MyTermsAsync());
        var afterCurrent = await person.AcceptTermsVersionAsync(current);
        var afterScheduled = await person.AcceptTermsVersionAsync(scheduled, "https://brand.example/terms/next");

        first.ShouldBe($"acceptance:{current}");
        TermsRequests.AskOf(await afterCurrent.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).ShouldBe($"acceptance:{scheduled}");
        TermsRequests.AskOf(await afterScheduled.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).ShouldBe("nothing");
    }

    [Fact]
    public async Task GetMyTerms_AVersionRequiringNoAcceptance_GivesTheNoticeUntilItIsClosed()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var versionId = await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), summary: "New", requiresAcceptance: false));

        var before = await person.MyTermsAsync();
        var dismissed = await person.DismissTermsNoticeAsync(versionId);

        TermsRequests.AskOf(before).ShouldBe($"notice:{versionId}");
        before.GetProperty("version").GetProperty("summary").GetString().ShouldBe("New");
        TermsRequests.AskOf(await dismissed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).ShouldBe("nothing");
        TermsRequests.AskOf(await person.MyTermsAsync()).ShouldBe("nothing");
    }

    // A notice tells of a change: a person who became a user after the version knew no earlier terms.
    [Fact]
    public async Task GetMyTerms_AUserNewerThanTheNoticeOnlyVersion_MustDoNothing()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.Api.WaitingForMessagesAsync(() => own.Admin.AddTermsVersionAsync(TermsRequests.Today, requiresAcceptance: false));
        var (_, person) = await own.AddTenantUserAsync(tenantId);

        TermsRequests.AskOf(await person.MyTermsAsync()).ShouldBe("nothing");
    }

    [Fact]
    public async Task GetMyTerms_APersonWhoIsNotAUserYet_MustDoNothing()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        await own.Admin.AddTermsVersionAsync(TermsRequests.Today);

        TermsRequests.AskOf(await own.Api.CreateClient($"user_{Guid.NewGuid():N}").MyTermsAsync()).ShouldBe("nothing");
    }
}
