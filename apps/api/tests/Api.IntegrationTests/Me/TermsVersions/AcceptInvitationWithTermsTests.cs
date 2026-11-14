using System.Net;
using System.Net.Http.Json;

namespace Api.IntegrationTests.Me.TermsVersions;

// POST /v1/me/invitations/accept with termsVersionId: an invitee who has no account yet ticks the terms on Accept invitation (1.15), and
// accepts the version in force in the same transaction as the invitation.
public sealed class AcceptInvitationWithTermsTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AcceptInvitation_TheVersionInForce_RecordsItsAcceptanceForTheNewUser()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);
        var (invitee, code) = await InviteAsync(own);

        var accepted = await AcceptAsync(own, invitee, code, versionId);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await own.ScalarAsync<long>(
            $"SELECT count(*) FROM control_plane.terms_acceptances JOIN control_plane.users ON users.id = user_id WHERE external_id = '{invitee}' AND version_id = '{versionId}'"))
            .ShouldBe(1);
        TermsRequests.AskOf(await own.Api.CreateClient(invitee).MyTermsAsync()).ShouldBe("none");
    }

    // The checkbox covers the version in force only; a scheduled one is the gate's, after sign-in.
    [Fact]
    public async Task AcceptInvitation_AScheduledVersion_IsAConflictAndAddsNothing()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));
        var (invitee, code) = await InviteAsync(own);

        var accepted = await AcceptAsync(own, invitee, code, versionId);

        (accepted.StatusCode, await TermsRequests.CodeOfAsync(accepted)).ShouldBe((HttpStatusCode.Conflict, "terms_version.changed"));
        (await own.ScalarAsync<long>($"SELECT count(*) FROM control_plane.users WHERE external_id = '{invitee}'")).ShouldBe(0);
    }

    private static async Task<(string Invitee, string Code)> InviteAsync(OwnTerms own)
    {
        var invitee = $"user_{Guid.NewGuid():N}";
        var email = TestEmails.Of($"{Guid.NewGuid():N}@example.com");
        own.Api.Identity.AddAccount(invitee, email);
        var slug = $"tenant-{Guid.NewGuid():N}"[..20];
        (await own.Api.WaitingForMessagesAsync(() => own.Admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail = email, language = "en" }))).EnsureSuccessStatusCode();
        return (invitee, own.Api.Email.CodeSentTo(email));
    }

    private static Task<HttpResponseMessage> AcceptAsync(OwnTerms own, string invitee, string code, Guid termsVersionId) =>
        own.Api.CreateClient(invitee).PostAsJsonAsync("/v1/me/invitations/accept", new { code, name = "Ada Lovelace", termsVersionId }, Cancellation);
}
