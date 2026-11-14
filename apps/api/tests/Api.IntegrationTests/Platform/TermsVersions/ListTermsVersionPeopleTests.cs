using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Platform.TermsVersions;

// GET /v1/platform/terms/versions/{versionId}/people: the users who may accept and accepted the version, the latest first, or those who did
// not yet, the newest first, by a cursor (K5's exception).
public sealed class ListTermsVersionPeopleTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListTermsVersionPeople_AcceptedAndNotYet_PagesEachByTheCursor()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (firstId, first) = await own.AddTenantUserAsync(tenantId);
        var (secondId, second) = await own.AddTenantUserAsync(tenantId);
        var (pendingId, _) = await own.AddTenantUserAsync(tenantId);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));
        (await first.AcceptTermsVersionAsync(versionId)).EnsureSuccessStatusCode();
        (await second.AcceptTermsVersionAsync(versionId)).EnsureSuccessStatusCode();

        var page = await (await own.Admin.TermsVersionPeopleAsync(versionId, "?status=accepted&limit=1")).Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var next = await (await own.Admin.TermsVersionPeopleAsync(versionId, $"?status=accepted&limit=1&cursor={page.GetProperty("nextCursor").GetString()}"))
            .Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var notYet = await (await own.Admin.TermsVersionPeopleAsync(versionId, "?status=not_yet")).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        PeopleOf(page).ShouldBe([$"{await own.Data.UserIdOfAsync(secondId)} accepted"]);
        PeopleOf(next).ShouldBe([$"{await own.Data.UserIdOfAsync(firstId)} accepted"]);
        next.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
        PeopleOf(notYet).ShouldBe([$"{await own.Data.UserIdOfAsync(pendingId)} not_yet"]);
        notYet.GetProperty("items")[0].GetProperty("acceptedAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?status=everyone")]
    public async Task ListTermsVersionPeople_NoStatusOrAnother_IsInvalid(string query)
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));

        var listed = await own.Admin.TermsVersionPeopleAsync(versionId, query);

        (listed.StatusCode, await TermsRequests.CodeOfAsync(listed)).ShouldBe((HttpStatusCode.BadRequest, "paging.filter_invalid"));
    }

    private static string[] PeopleOf(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(person => $"{person.GetProperty("userId").GetGuid()} {person.GetProperty("status").GetString()}")];
}
