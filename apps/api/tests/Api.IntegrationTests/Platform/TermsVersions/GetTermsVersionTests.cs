using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Platform.TermsVersions;

// GET /v1/platform/terms/versions/{versionId}: one version with its state and acceptances, and how many users may accept.
public sealed class GetTermsVersionTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetTermsVersion_AVersion_AnswersItWithTheActiveUsers()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.AddTenantUserAsync(tenantId);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));

        var found = await own.Admin.GetFromJsonAsync<JsonElement>($"/v1/platform/terms/versions/{versionId}", Cancellation);

        (found.GetProperty("version").GetProperty("id").GetGuid(), found.GetProperty("activeUserCount").GetInt32()).ShouldBe((versionId, 1));
    }

    [Fact]
    public async Task GetTermsVersion_NoVersionWithTheId_IsNotFound()
    {
        await using var own = await OwnTerms.CreateAsync(database);

        var found = await own.Admin.GetAsync($"/v1/platform/terms/versions/{Guid.NewGuid()}", Cancellation);

        (found.StatusCode, await TermsRequests.CodeOfAsync(found)).ShouldBe((HttpStatusCode.NotFound, "terms_version.not_found"));
    }
}
