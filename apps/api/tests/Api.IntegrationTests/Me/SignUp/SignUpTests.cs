using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Me.SignUp;

// Step 40d: a person who signed up at the identity provider becomes a user, while the product has sign-up on. The address is the one the
// identity provider verified; the person gives no name (1.25 to 1.27).
public sealed class SignUpTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private ApiFactory _off = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: SignUpSettings.On);
        _off = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _api.DisposeAsync();
        await _off.DisposeAsync();
    }

    [Fact]
    public async Task SignUp_APersonWhoIsNotAUserYet_BecomesAUserWithTheVerifiedAddressTheLanguageAndNoName()
    {
        var (person, email) = NewAccount(_api);

        var signedUp = await _api.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { language = "tr" }, Cancellation);

        signedUp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await signedUp.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (me.GetProperty("email").GetString(), me.GetProperty("name").ValueKind, me.GetProperty("language").GetString(), me.GetProperty("isSystemAdmin").GetBoolean())
            .ShouldBe((email, JsonValueKind.Null, "tr", false));
        me.GetProperty("id").GetGuid().ShouldBe(await _testData.UserIdOfAsync(person));
    }

    // The product has sign-up off by default: only an invitation makes a user (A7).
    [Fact]
    public async Task SignUp_SignUpOff_IsForbiddenAndAddsNoUser()
    {
        var (person, _) = NewAccount(_off);

        var signedUp = await _off.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { language = "en" }, Cancellation);

        (signedUp.StatusCode, await CodeOfAsync(signedUp)).ShouldBe((HttpStatusCode.Forbidden, "sign_up.disabled"));
        (await UsersOfAsync(person)).ShouldBe(0);
    }

    // A retry after a lost answer finds the user it made: it sets a state, so it answers the user and changes nothing.
    [Fact]
    public async Task SignUp_AKnownUser_AnswersThemAndChangesNothing()
    {
        var (person, email) = NewAccount(_api);
        (await _api.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { language = "tr" }, Cancellation)).EnsureSuccessStatusCode();

        var again = await _api.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { language = "en" }, Cancellation);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await again.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (me.GetProperty("email").GetString(), me.GetProperty("language").GetString()).ShouldBe((email, "tr"));
        (await UsersOfAsync(person)).ShouldBe(1);
    }

    // The unique index of the addresses (A7): another live user keeps the verified address.
    [Fact]
    public async Task SignUp_AnAddressAnotherUserKeeps_IsAConflictAndAddsNoUser()
    {
        var (person, email) = NewAccount(_api);
        await _testData.AddUserAsync(email: email);

        var signedUp = await _api.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { language = "en" }, Cancellation);

        (signedUp.StatusCode, await CodeOfAsync(signedUp)).ShouldBe((HttpStatusCode.Conflict, "user.email_taken"));
        (await UsersOfAsync(person)).ShouldBe(0);
    }

    // The identity provider verifies the address at sign-up (.clerk/config.json); an account without one gets no user.
    [Fact]
    public async Task SignUp_NoVerifiedAddress_IsForbiddenAndAddsNoUser()
    {
        var person = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(person);

        var signedUp = await _api.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { language = "en" }, Cancellation);

        (signedUp.StatusCode, await CodeOfAsync(signedUp)).ShouldBe((HttpStatusCode.Forbidden, "user.email_unverified"));
        (await UsersOfAsync(person)).ShouldBe(0);
    }

    [Fact]
    public async Task SignUp_WithoutALanguage_IsABadRequest()
    {
        var (person, _) = NewAccount(_api);

        var signedUp = await _api.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { }, Cancellation);

        signedUp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await UsersOfAsync(person)).ShouldBe(0);
    }

    // 1.27: the person accepts the terms in force to create the account, in the same transaction (GDPR, Article 7(1)).
    [Fact]
    public async Task SignUp_WithTheTermsInForce_RecordsTheAcceptance()
    {
        await using var own = await OwnTerms.CreateAsync(database, SignUpSettings.On);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);
        var (person, _) = NewAccount(own.Api);

        var signedUp = await own.Api.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { language = "en", termsVersionId = versionId }, Cancellation);

        signedUp.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await own.ScalarAsync<long>(
            $"SELECT count(*) FROM control_plane.terms_acceptances JOIN control_plane.users ON users.id = user_id WHERE external_id = '{person}' AND version_id = '{versionId}'"))
            .ShouldBe(1);
        TermsRequests.AskOf(await own.Api.CreateClient(person).MyTermsAsync()).ShouldBe("nothing");
    }

    // The person ticked a version that is not in force: they read the terms again, and no user is made.
    [Fact]
    public async Task SignUp_WithAVersionNotInForce_IsAConflictAndAddsNoUser()
    {
        await using var own = await OwnTerms.CreateAsync(database, SignUpSettings.On);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));
        var (person, _) = NewAccount(own.Api);

        var signedUp = await own.Api.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { language = "en", termsVersionId = versionId }, Cancellation);

        (signedUp.StatusCode, await TermsRequests.CodeOfAsync(signedUp)).ShouldBe((HttpStatusCode.Conflict, "terms_version.changed"));
        (await own.ScalarAsync<long>($"SELECT count(*) FROM control_plane.users WHERE external_id = '{person}'")).ShouldBe(0);
    }

    private static (string Person, string Email) NewAccount(ApiFactory api)
    {
        var (person, email) = ($"user_{Guid.NewGuid():N}", TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        api.Identity.AddAccount(person, email);
        return (person, email);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private Task<long> UsersOfAsync(string person) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.users WHERE external_id = '{person}'");
}
