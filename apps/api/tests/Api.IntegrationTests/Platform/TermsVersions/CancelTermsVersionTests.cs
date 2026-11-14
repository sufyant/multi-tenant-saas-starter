using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Platform.TermsVersions;

// POST /v1/platform/terms/versions/{versionId}/cancel: the scheduled version is cancelled before its day, and keeps its acceptances.
public sealed class CancelTermsVersionTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CancelTermsVersion_AnAcceptedScheduledVersion_KeepsItsAcceptancesAndRecordsTheirCount()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));
        (await person.AcceptTermsVersionAsync(versionId)).EnsureSuccessStatusCode();

        var cancelled = await own.Api.WaitingForMessagesAsync(() => own.Admin.CancelTermsVersionAsync(versionId));

        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var version = await cancelled.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (version.GetProperty("state").GetString(), version.GetProperty("acceptedCount").GetInt32()).ShouldBe(("cancelled", 1));
        (await own.ScalarAsync<string>($"SELECT details->>'acceptanceCount' FROM audit.platform_entries WHERE target_id = '{versionId}' AND operation = 'terms_version.cancelled'"))
            .ShouldBe("1");
    }

    // A cancelled version answers as it is, and the second cancellation records nothing.
    [Fact]
    public async Task CancelTermsVersion_ACancelledVersion_AnswersItAgainAndRecordsOnce()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));
        await own.Api.WaitingForMessagesAsync(() => own.Admin.CancelTermsVersionAsync(versionId));

        var again = await own.Api.WaitingForMessagesAsync(() => own.Admin.CancelTermsVersionAsync(versionId));

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await own.ScalarAsync<long>($"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{versionId}' AND operation = 'terms_version.cancelled'")).ShouldBe(1);
    }

    [Fact]
    public async Task CancelTermsVersion_AVersionInForce_IsNotScheduled()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);

        var cancelled = await own.Admin.CancelTermsVersionAsync(versionId);

        (cancelled.StatusCode, await TermsRequests.CodeOfAsync(cancelled)).ShouldBe((HttpStatusCode.Conflict, "terms_version.not_scheduled"));
    }
}
