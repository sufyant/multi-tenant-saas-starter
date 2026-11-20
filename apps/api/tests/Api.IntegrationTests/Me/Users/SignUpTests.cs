using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Me.Users;

// Step 40d: a person who signed up at the identity provider becomes a user, while the product has sign-up on. The address is the one the
// identity provider verified; the person gives no name (1.25 to 1.27).
public sealed class SignUpTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

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

    // K3: the new user acted, in the platform's log; a user belongs to no tenant.
    [Fact]
    public async Task SignUp_APersonWhoIsNotAUserYet_IsRecordedInThePlatformLogBySelf()
    {
        var (person, _) = NewAccount(_api);

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { language = "en" }, Cancellation));

        var userId = await _testData.UserIdOfAsync(person);
        (await _audit.PlatformLogAsync(userId)).ShouldBe($"user.signed_up|User|{userId}|User|{userId}|-");
    }

    // K6f: two sign-ups of one person at once. Neither finds a user row to lock, so the first to read the identity provider id makes the
    // second wait, which then finds the first one's user. A holder adds a user with the same id and keeps it uncommitted until both wait
    // behind it, and then rolls it back. Without the lock, the unique index of the identity provider id refuses the second with 500.
    [Fact]
    public async Task SignUp_TwoAtOnce_MakesOneUserAndAnswersBoth()
    {
        var (person, _) = NewAccount(_api);
        await using var holder = new NpgsqlConnection(database.SuperuserConnectionString);
        await holder.OpenAsync(Cancellation);
        await using var holding = await holder.BeginTransactionAsync(Cancellation);
        await using var hold = new NpgsqlCommand(
            $"INSERT INTO control_plane.users (id, external_id, language) VALUES ('{Guid.NewGuid()}', '{person}', 'en')", holder, holding);
        await hold.ExecuteNonQueryAsync(Cancellation);

        var signingUp = _api.WaitingForMessagesAsync(() => Task.WhenAll(
            _api.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { language = "en" }, Cancellation),
            _api.CreateClient(person).PostAsJsonAsync("/v1/me/sign-up", new { language = "en" }, Cancellation)));
        await Waiting.UntilAsync(async () => signingUp.IsCompleted || await WaitingBehindAsync(holder.ProcessID) == 2);
        await holding.RollbackAsync(Cancellation);
        var signedUp = await signingUp;

        signedUp.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.OK]);
        (await UsersOfAsync(person)).ShouldBe(1);
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

    private Task<long> WaitingBehindAsync(int holder) =>
        database.ScalarAsSuperuserAsync<long>(
            $"""
            WITH RECURSIVE behind (pid) AS (
                SELECT pid FROM pg_stat_activity WHERE {holder} = ANY(pg_blocking_pids(pid))
                UNION SELECT waiting.pid FROM pg_stat_activity AS waiting JOIN behind ON behind.pid = ANY(pg_blocking_pids(waiting.pid)))
            SELECT count(*) FROM behind
            """);
}
