using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Public;

// GET /v1/public/terms: anyone reads the version in force without signing in; before the first version takes effect, 204 hides the link.
public sealed class GetTermsInForceTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetTermsInForce_OnlyAScheduledVersion_AnswersNoContentToAnyone()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));

        var answer = await own.Api.CreateClient().GetAsync("/v1/public/terms", Cancellation);

        answer.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetTermsInForce_AVersionInForce_AnswersItsLinkForAnyCache()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);
        await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), "https://brand.example/terms/next");

        var answer = await own.Api.CreateClient().GetAsync("/v1/public/terms", Cancellation);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var version = await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (version.GetProperty("id").GetGuid(), version.GetProperty("url").GetString(), version.GetProperty("effectiveDate").GetString())
            .ShouldBe((versionId, TermsRequests.Link, $"{TermsRequests.Today:yyyy-MM-dd}"));
        answer.Headers.CacheControl!.ToString().ShouldBe("public, max-age=300");
    }
}
