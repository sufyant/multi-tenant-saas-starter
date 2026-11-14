using System.Net;

namespace Api.IntegrationTests.Me.TermsVersions;

// POST /v1/me/terms/versions/{versionId}/accept: the person accepts the version in force or the scheduled one, with the link they read.
public sealed class AcceptTermsVersionTests(Database database)
{
    // A person accepts the text they read: a link that changed since, or a version that another replaced, is refused.
    [Fact]
    public async Task AcceptTermsVersion_ALinkThatChanged_IsAConflictAndRecordsNothing()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));
        (await own.Admin.UpdateTermsVersionAsync(versionId, TermsRequests.Today.AddDays(10), "https://brand.example/terms/fixed")).EnsureSuccessStatusCode();

        var accepted = await person.AcceptTermsVersionAsync(versionId);

        (accepted.StatusCode, await TermsRequests.CodeOfAsync(accepted)).ShouldBe((HttpStatusCode.Conflict, "terms_version.changed"));
        (await own.ScalarAsync<long>("SELECT count(*) FROM control_plane.terms_acceptances")).ShouldBe(0);
    }

    [Fact]
    public async Task AcceptTermsVersion_AReplacedVersion_IsAConflict()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var replaced = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);
        await own.Admin.AddTermsVersionAsync(TermsRequests.Today);

        var accepted = await person.AcceptTermsVersionAsync(replaced);

        (accepted.StatusCode, await TermsRequests.CodeOfAsync(accepted)).ShouldBe((HttpStatusCode.Conflict, "terms_version.changed"));
    }

    // A second acceptance keeps the first moment (I1).
    [Fact]
    public async Task AcceptTermsVersion_Twice_KeepsOneAcceptance()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);

        (await person.AcceptTermsVersionAsync(versionId)).EnsureSuccessStatusCode();
        var again = await person.AcceptTermsVersionAsync(versionId);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await own.ScalarAsync<long>($"SELECT count(*) FROM control_plane.terms_acceptances WHERE version_id = '{versionId}'")).ShouldBe(1);
    }
}
