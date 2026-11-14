using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Platform.TermsVersions;

// GET /v1/platform/terms and GET /v1/platform/terms/versions/{versionId}: every version with its state on the day of the request, its
// acceptances, and how many users may accept. A user who may accept is a tenant user who is active: a suspended user, a deleted user and a
// system admin who joined no tenant count for nothing.
public sealed class GetTermsOfUseTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetTermsOfUse_VersionsOfEveryState_AnswersEachStateAndCountsTheActiveTenantUsers()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, first) = await own.AddTenantUserAsync(tenantId);
        await own.AddTenantUserAsync(tenantId);
        var (suspended, suspendedClient) = await own.AddTenantUserAsync(tenantId);
        var (deleted, _) = await own.AddTenantUserAsync(tenantId);
        await own.Data.DeleteUserAsync(deleted);
        var replaced = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);
        var current = await own.Admin.AddTermsVersionAsync(TermsRequests.Today, "https://brand.example/terms/now");
        var dropped = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(5));
        await own.Admin.CancelTermsVersionAsync(dropped);
        var scheduled = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));
        (await first.AcceptTermsVersionAsync(current, "https://brand.example/terms/now")).EnsureSuccessStatusCode();
        (await suspendedClient.AcceptTermsVersionAsync(current, "https://brand.example/terms/now")).EnsureSuccessStatusCode();
        await own.Data.SuspendUserAsync(suspended);

        var terms = await own.Admin.TermsOfUseAsync();

        terms.GetProperty("activeUserCount").GetInt32().ShouldBe(2);
        terms.GetProperty("versions").EnumerateArray()
            .Select(version => $"{version.GetProperty("id").GetGuid()} {version.GetProperty("state").GetString()} {version.GetProperty("acceptedCount").GetInt32()} {version.GetProperty("locked").GetBoolean()}")
            .ShouldBe([$"{scheduled} scheduled 0 False", $"{dropped} cancelled 0 False", $"{current} current 1 True", $"{replaced} replaced 0 False"]);
    }

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
