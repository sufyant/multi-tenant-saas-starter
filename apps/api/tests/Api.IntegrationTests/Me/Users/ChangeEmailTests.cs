using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Me.Users;

// POST /v1/me/email-change/confirm: with the code that went to the new address, the API changes our record, replaces the address at the
// identity provider through the outbox, records user.email_changed and tells the old address, with the new one masked (A7). The code
// works once, for 10 minutes, and for 5 wrong tries (section 6, "Verification codes").
public sealed class ChangeEmailTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    // The clock starts at the real now, so a scheduled message of a test comes long after the run (section 9, rule 10).
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task ChangeEmail_TheRightCode_ChangesTheAddressHereAndAtTheIdentityProvider()
    {
        var (user, newEmail) = await UserWithACodeAsync();
        _api.Identity.AddAccount(user, "ada@example.com");

        var changed = await _api.WaitingForMessagesAsync(() => AccountRequests.ChangeEmailAsync(_api, user, AccountRequests.CodeSentTo(_api, newEmail)));

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await changed.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("email").GetString().ShouldBe(newEmail);
        (await _testData.UserOfAsync(user)).Email.ShouldBe(newEmail);
        _api.Identity.Replaced.ShouldBe([(user, newEmail)]);
    }

    // The old address learns where the account went, without the whole new address, and the platform's log records the change, by the
    // user, with no address (K3).
    [Fact]
    public async Task ChangeEmail_TheRightCode_TellsTheOldAddressAndRecordsTheChange()
    {
        var (user, newEmail) = await UserWithACodeAsync();
        var userId = await _testData.UserIdOfAsync(user);

        await _api.WaitingForMessagesAsync(() => AccountRequests.ChangeEmailAsync(_api, user, AccountRequests.CodeSentTo(_api, newEmail)));

        var notice = _api.Email.Sent.Single(sent => sent.To == "ada@example.com");
        notice.Subject.ShouldBe("Your email address was changed");
        notice.Text.ShouldContain("a***@new.example");
        notice.Text.ShouldNotContain(newEmail);
        (await _audit.PlatformLogAsync(userId)).ShouldBe($"user.email_changed|User|{userId}|User|{userId}|-");
    }

    // The identity provider's call is retried durably, so our record and the provider do not stay apart.
    [Fact]
    public async Task ChangeEmail_TheIdentityProviderFailsTwice_ReplacesTheAddressOnTheThirdTry()
    {
        var (user, newEmail) = await UserWithACodeAsync();
        _api.Identity.FailNext(2);

        await _api.TrackMessagesAsync(() => AccountRequests.ChangeEmailAsync(_api, user, AccountRequests.CodeSentTo(_api, newEmail)));

        _api.Identity.Replaced.ShouldBe([(user, newEmail)]);
    }

    // A wrong code changes nothing, and counts a try; after 5 wrong tries the right code is refused too, and the person asks for a new one.
    [Fact]
    public async Task ChangeEmail_FiveWrongCodes_RefuseTheRightOneAfterThem()
    {
        var (user, newEmail) = await UserWithACodeAsync();
        var right = AccountRequests.CodeSentTo(_api, newEmail);
        var tries = await TryTimesAsync(user, "no-code", 5);
        var last = await AccountRequests.ChangeEmailAsync(_api, user, right);

        tries.ShouldAllBe(code => code == "verification_code.invalid");
        (last.StatusCode, await AccountRequests.CodeOfAsync(last)).ShouldBe((HttpStatusCode.BadRequest, "verification_code.too_many_attempts"));
        (await _testData.UserOfAsync(user)).Email.ShouldBe("ada@example.com");
    }

    [Fact]
    public async Task ChangeEmail_After10Minutes_IsRefusedAsExpired()
    {
        var (user, newEmail) = await UserWithACodeAsync();
        _time.Advance(TimeSpan.FromMinutes(10));

        var changed = await AccountRequests.ChangeEmailAsync(_api, user, AccountRequests.CodeSentTo(_api, newEmail));

        (changed.StatusCode, await AccountRequests.CodeOfAsync(changed)).ShouldBe((HttpStatusCode.BadRequest, "verification_code.expired"));
        (await _testData.UserOfAsync(user)).Email.ShouldBe("ada@example.com");
    }

    // A code works once.
    [Fact]
    public async Task ChangeEmail_TheSameCodeTwice_RefusesTheSecond()
    {
        var (user, newEmail) = await UserWithACodeAsync();
        var code = AccountRequests.CodeSentTo(_api, newEmail);
        await _api.WaitingForMessagesAsync(() => AccountRequests.ChangeEmailAsync(_api, user, code));

        var again = await AccountRequests.ChangeEmailAsync(_api, user, code);

        (again.StatusCode, await AccountRequests.CodeOfAsync(again)).ShouldBe((HttpStatusCode.BadRequest, "verification_code.invalid"));
    }

    // An address that another account took after the code went out is refused, and the person keeps theirs.
    [Fact]
    public async Task ChangeEmail_AnotherAccountTookTheAddressMeanwhile_IsAConflict()
    {
        var (user, newEmail) = await UserWithACodeAsync();
        await _testData.AddUserAsync(email: newEmail);

        var changed = await AccountRequests.ChangeEmailAsync(_api, user, AccountRequests.CodeSentTo(_api, newEmail));

        (changed.StatusCode, await AccountRequests.CodeOfAsync(changed)).ShouldBe((HttpStatusCode.Conflict, "user.email_taken"));
        (await _testData.UserOfAsync(user)).Email.ShouldBe("ada@example.com");
    }

    private async Task<(string User, string NewEmail)> UserWithACodeAsync()
    {
        var user = await _testData.AddUserAsync(email: "ada@example.com", firstName: "Ada", lastName: "Lovelace");
        var newEmail = $"a{Guid.NewGuid():N}@new.example";
        (await _api.WaitingForMessagesAsync(() => AccountRequests.RequestEmailChangeAsync(_api, user, newEmail))).StatusCode.ShouldBe(HttpStatusCode.Created);
        return (user, newEmail);
    }

    private async Task<List<string?>> TryTimesAsync(string user, string code, int times)
    {
        var codes = new List<string?>();
        for (var attempt = 0; attempt < times; attempt++)
        {
            codes.Add(await AccountRequests.CodeOfAsync(await AccountRequests.ChangeEmailAsync(_api, user, code)));
        }

        return codes;
    }
}
