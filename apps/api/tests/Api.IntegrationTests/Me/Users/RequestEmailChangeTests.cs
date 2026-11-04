using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tenancy;

namespace Api.IntegrationTests.Me.Users;

// POST /v1/me/email-change: the person gives a new address, and the API sends a code of six digits to it, our email, not the identity
// provider's (A7; section 6, "Verification codes"). An address that another account uses, or the person's own, is refused at once.
public sealed class RequestEmailChangeTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // The code goes to the new address, in the person's language, and works for 10 minutes; ControlPlane keeps only its hash.
    [Fact]
    public async Task RequestEmailChange_ANewAddress_SendsACodeToItThatWorksForTenMinutes()
    {
        var user = await _testData.AddUserAsync(email: TestEmails.Of("ada@example.com"), language: "tr");
        var newEmail = $"ada-{Guid.NewGuid():N}@new.example";

        var asked = DateTimeOffset.UtcNow;
        var requested = await _api.WaitingForMessagesAsync(() => AccountRequests.RequestEmailChangeAsync(_api, user, $" {newEmail.ToUpperInvariant()} "));

        requested.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await requested.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("expiresAt").GetDateTimeOffset()
            .ShouldBeInRange(asked.AddMinutes(10), DateTimeOffset.UtcNow.AddMinutes(10));
        var email = _api.Email.Sent.ShouldHaveSingleItem();
        (email.To, email.Subject).ShouldBe((newEmail, "E-posta adresinizi değiştirme kodunuz"));
        AccountRequests.CodeSentTo(_api, newEmail).Length.ShouldBe(6);
        (await database.ScalarAsSuperuserAsync<int>($"SELECT length(code_hash) FROM control_plane.verification_codes WHERE new_email = '{newEmail}'")).ShouldBe(32);
        (await _testData.UserOfAsync(user)).Email.ShouldBe(TestEmails.Of("ada@example.com"));
    }

    [Fact]
    public async Task RequestEmailChange_AnAddressAnotherAccountUses_IsAConflictAndSendsNothing()
    {
        var taken = $"grace-{Guid.NewGuid():N}@example.com";
        await _testData.AddUserAsync(email: taken);
        var user = await _testData.AddUserAsync(email: TestEmails.Of("ada@example.com"));

        var requested = await _api.WaitingForMessagesAsync(() => AccountRequests.RequestEmailChangeAsync(_api, user, taken));

        requested.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AccountRequests.CodeOfAsync(requested)).ShouldBe("user.email_taken");
        _api.Email.Sent.ShouldBeEmpty();
    }

    // Ayşe was invited, signed up at the identity provider, and never joined: no user of ours has her address, but her account there does.
    [Fact]
    public async Task RequestEmailChange_AnAddressAnotherAccountUsesAtTheIdentityProvider_IsAConflictAndSendsNothing()
    {
        var taken = $"ayse-{Guid.NewGuid():N}@example.com";
        _api.Identity.AddAccount($"user_{Guid.NewGuid():N}", taken);
        var user = await _testData.AddUserAsync(email: TestEmails.Of("ada@example.com"));

        var requested = await _api.WaitingForMessagesAsync(() => AccountRequests.RequestEmailChangeAsync(_api, user, taken));

        requested.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AccountRequests.CodeOfAsync(requested)).ShouldBe("user.email_taken");
        _api.Email.Sent.ShouldBeEmpty();
    }

    // I5 and I6: the identity provider is asked under the key, so its refusal is saved, and a replay answers it again without asking.
    [Fact]
    public async Task RequestEmailChange_AnAddressTakenAtTheIdentityProviderReplayed_ReplaysTheSavedConflict()
    {
        var taken = $"ayse-{Guid.NewGuid():N}@example.com";
        _api.Identity.AddAccount($"user_{Guid.NewGuid():N}", taken);
        var user = await _testData.AddUserAsync(email: TestEmails.Of("ada@example.com"));
        var key = Guid.NewGuid().ToString();
        (await AccountRequests.RequestEmailChangeAsync(_api, user, taken, key)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        _api.Identity.IsDown = true;

        var again = await AccountRequests.RequestEmailChangeAsync(_api, user, taken, key);

        (again.StatusCode, await AccountRequests.CodeOfAsync(again)).ShouldBe((HttpStatusCode.Conflict, "user.email_taken"));
        again.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
    }

    // I5: a finished request replays its saved result, so a replay does not depend on the identity provider.
    [Fact]
    public async Task RequestEmailChange_ReplayedWhileTheIdentityProviderIsDown_ReplaysTheSavedResult()
    {
        var user = await _testData.AddUserAsync(email: TestEmails.Of("ada@example.com"));
        var newEmail = $"a{Guid.NewGuid():N}@new.example";
        var key = Guid.NewGuid().ToString();
        var first = await _api.WaitingForMessagesAsync(() => AccountRequests.RequestEmailChangeAsync(_api, user, newEmail, key));
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        _api.Identity.IsDown = true;

        var again = await AccountRequests.RequestEmailChangeAsync(_api, user, newEmail, key);

        again.StatusCode.ShouldBe(HttpStatusCode.Created);
        again.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await again.Content.ReadAsStringAsync(Cancellation)).ShouldBe(await first.Content.ReadAsStringAsync(Cancellation));
    }

    // The identity provider's failure answers as for its other calls in a request: a server error, and no code goes out.
    [Fact]
    public async Task RequestEmailChange_TheIdentityProviderIsDown_IsAServerErrorAndSendsNothing()
    {
        var user = await _testData.AddUserAsync(email: TestEmails.Of("ada@example.com"));
        _api.Identity.IsDown = true;

        var requested = await AccountRequests.RequestEmailChangeAsync(_api, user, $"a{Guid.NewGuid():N}@new.example");

        requested.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await AccountRequests.CodeOfAsync(requested)).ShouldBe("server.error");
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.verification_codes v JOIN control_plane.users u ON u.id = v.user_id WHERE u.external_id = '{user}'")).ShouldBe(0);
        _api.Email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task RequestEmailChange_ThePersonsOwnAddress_IsAConflict()
    {
        var own = $"ada-{Guid.NewGuid():N}@example.com";
        var user = await _testData.AddUserAsync(email: own);

        var requested = await AccountRequests.RequestEmailChangeAsync(_api, user, own);

        requested.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AccountRequests.CodeOfAsync(requested)).ShouldBe("user.email_unchanged");
    }

    // At most 5 codes of a purpose in an hour, so nobody floods an address with codes.
    [Fact]
    public async Task RequestEmailChange_ASixthCodeWithinAnHour_IsTooManyRequests()
    {
        var user = await _testData.AddUserAsync(email: TestEmails.Of("ada@example.com"));
        var newEmail = $"ada-{Guid.NewGuid():N}@new.example";
        (await RequestTimesAsync(user, newEmail, 5)).ShouldAllBe(status => status == HttpStatusCode.Created);

        var sixth = await AccountRequests.RequestEmailChangeAsync(_api, user, newEmail);

        sixth.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await AccountRequests.CodeOfAsync(sixth)).ShouldBe("verification_code.too_many_sends");
    }

    [Theory]
    [InlineData("", "required")]
    [InlineData("not an address", "email_invalid")]
    public async Task RequestEmailChange_AWrongAddress_IsABadRequest(string email, string fieldCode)
    {
        var user = await _testData.AddUserAsync(email: TestEmails.Of("ada@example.com"));

        var requested = await AccountRequests.RequestEmailChangeAsync(_api, user, email);

        requested.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await requested.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("errors").GetProperty("email")[0].GetString().ShouldBe(fieldCode);
    }

    private async Task<List<HttpStatusCode>> RequestTimesAsync(string user, string email, int times)
    {
        var statuses = new List<HttpStatusCode>();
        for (var request = 0; request < times; request++)
        {
            statuses.Add((await AccountRequests.RequestEmailChangeAsync(_api, user, email)).StatusCode);
        }

        return statuses;
    }
}
