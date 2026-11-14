using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Platform.TermsVersions;

// PATCH /v1/platform/terms/versions/{versionId}: the scheduled version's values. Once someone accepted it, its link and whether it
// requires acceptance lock; its day and summary still change (the maintainer's decision of 2026-11-14).
public sealed class UpdateTermsVersionTests(Database database)
{
    private const string NewLink = "https://brand.example/terms/2026-v2";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UpdateTermsVersion_NobodyAccepted_TakesTheNewLinkAndRecordsBoth()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));

        var updated = await own.Api.WaitingForMessagesAsync(() => own.Admin.UpdateTermsVersionAsync(versionId, TermsRequests.Today.AddDays(12), NewLink, "New", false));

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var version = await updated.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (version.GetProperty("url").GetString(), version.GetProperty("requiresAcceptance").GetBoolean()).ShouldBe((NewLink, false));
        (await own.ScalarAsync<string>($"SELECT details->'old'->>'url' || ' ' || (details->'new'->>'url') FROM audit.platform_entries WHERE target_id = '{versionId}' AND operation = 'terms_version.updated'"))
            .ShouldBe($"{TermsRequests.Link} {NewLink}");
    }

    [Theory]
    [InlineData(NewLink, true)]
    [InlineData(TermsRequests.Link, false)]
    public async Task UpdateTermsVersion_SomeoneAcceptedAndALockedField_IsAConflictAndKeepsTheVersion(string url, bool requiresAcceptance)
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));
        (await person.AcceptTermsVersionAsync(versionId)).EnsureSuccessStatusCode();

        var updated = await own.Admin.UpdateTermsVersionAsync(versionId, TermsRequests.Today.AddDays(12), url, null, requiresAcceptance);

        (updated.StatusCode, await TermsRequests.CodeOfAsync(updated)).ShouldBe((HttpStatusCode.Conflict, "terms_version.locked"));
        (await own.ScalarAsync<string>($"SELECT url FROM control_plane.terms_versions WHERE id = '{versionId}'")).ShouldBe(TermsRequests.Link);
    }

    [Fact]
    public async Task UpdateTermsVersion_SomeoneAcceptedAndTheDayAndSummaryChange_TakesThem()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));
        (await person.AcceptTermsVersionAsync(versionId)).EnsureSuccessStatusCode();

        var updated = await own.Admin.UpdateTermsVersionAsync(versionId, TermsRequests.Today.AddDays(12), summary: "Later");

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var version = await updated.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (version.GetProperty("summary").GetString(), version.GetProperty("locked").GetBoolean(), version.GetProperty("acceptedCount").GetInt32()).ShouldBe(("Later", true, 1));
    }

    // A version in force never changes.
    [Fact]
    public async Task UpdateTermsVersion_AVersionInForce_IsNotScheduled()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);

        var updated = await own.Admin.UpdateTermsVersionAsync(versionId, TermsRequests.Today.AddDays(5));

        (updated.StatusCode, await TermsRequests.CodeOfAsync(updated)).ShouldBe((HttpStatusCode.Conflict, "terms_version.not_scheduled"));
    }

    [Fact]
    public async Task UpdateTermsVersion_NoVersionWithTheId_IsNotFound()
    {
        await using var own = await OwnTerms.CreateAsync(database);

        var updated = await own.Admin.UpdateTermsVersionAsync(Guid.NewGuid(), TermsRequests.Today.AddDays(5));

        (updated.StatusCode, await TermsRequests.CodeOfAsync(updated)).ShouldBe((HttpStatusCode.NotFound, "terms_version.not_found"));
    }
}
