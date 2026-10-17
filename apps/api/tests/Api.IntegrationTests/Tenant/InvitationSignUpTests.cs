using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Invitations.SignUp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Tenant;

// Clerk is invite-only, so a person without an account signs up only through Clerk's own invitation. The invitation stays ours: after it
// commits, ControlPlane asks Clerk for the sign-up link, records Clerk's invitation on ours and only then has Notifications email the
// link. When our invitation ends, Clerk's is withdrawn. Clerk is a fake at our port; the adapter's own requests are pinned down in
// ClerkIdentityProviderTests.
public sealed class InvitationSignUpTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // Clerk's invitation is created once, for our accept link and our expiry, and our email carries its link, which leads to the accept
    // link.
    [Fact]
    public async Task InviteMember_PersonWithoutAnAccount_CreatesOneClerkInvitationAndEmailsItsLink()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var email = NewEmail();

        var invited = await _api.WaitingForMessagesAsync(() => owner.InviteMemberAsync(tenantId, email));

        var invitation = await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        var signUp = _api.Identity.SignUps.Where(signUp => signUp.Email == email).ShouldHaveSingleItem();
        signUp.AcceptLink.ShouldBe(FakeIdentityProvider.AcceptLinkOf(_api.Email.LinkSentTo(email)));
        signUp.AcceptLink.GetLeftPart(UriPartial.Path).ShouldBe(ApiFactory.AcceptUrl);
        signUp.ExpiresAt.ShouldBe(invitation.GetProperty("expiresAt").GetDateTimeOffset());
        _api.Email.LinkSentTo(email).ShouldBe(FakeIdentityProvider.ClerkLink(signUp.InvitationId, signUp.AcceptLink));
        (await SignUpInvitationIdAsync(invitation.GetProperty("id").GetGuid())).ShouldBe(signUp.InvitationId);
    }

    // A person with an account signs in and accepts: Clerk would refuse an invitation for their address, so the email carries the accept
    // link itself.
    [Fact]
    public async Task InviteMember_PersonWithAnAccount_CreatesNoClerkInvitationAndEmailsTheAcceptLink()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var email = NewEmail();
        _api.Identity.AddAccount($"user_{Guid.NewGuid():N}", email);

        var invited = await _api.WaitingForMessagesAsync(() => owner.InviteMemberAsync(tenantId, email));

        var invitationId = (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        _api.Identity.SignUps.ShouldNotContain(signUp => signUp.Email == email);
        _api.Email.LinkSentTo(email).GetLeftPart(UriPartial.Path).ShouldBe(ApiFactory.AcceptUrl);
        (await SignUpInvitationIdAsync(invitationId)).ShouldBeNull();
    }

    [Fact]
    public async Task RevokeInvitation_WithAClerkInvitation_WithdrawsItOnce()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var (invitationId, signUp) = await InviteAsync(tenantId, owner);

        await _api.WaitingForMessagesAsync(() => owner.DeleteAsync($"/v1/tenants/{tenantId}/invitations/{invitationId}", Cancellation));
        await _api.WaitingForMessagesAsync(() => owner.DeleteAsync($"/v1/tenants/{tenantId}/invitations/{invitationId}", Cancellation));

        _api.Identity.Withdrawn.Where(withdrawn => withdrawn == signUp).ShouldHaveSingleItem();
    }

    // K2: a new invitation to the same address replaces the pending one; the replaced one's Clerk invitation is withdrawn, and the new one
    // gets its own.
    [Fact]
    public async Task InviteMember_ReplacingAPendingInvitation_WithdrawsTheReplacedClerkInvitationOnce()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var email = NewEmail();
        var (_, replaced) = await InviteAsync(tenantId, owner, email);

        await _api.WaitingForMessagesAsync(() => owner.InviteMemberAsync(tenantId, email));

        _api.Identity.Withdrawn.Where(withdrawn => withdrawn == replaced).ShouldHaveSingleItem();
        _api.Identity.SignUps.Count(signUp => signUp.Email == email).ShouldBe(2);
    }

    [Fact]
    public async Task CloseTenant_WithPendingInvitations_WithdrawsEachClerkInvitationOnce()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var (_, first) = await InviteAsync(tenantId, owner);
        var (_, second) = await InviteAsync(tenantId, owner);
        var admin = _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

        await _api.WaitingForMessagesAsync(() => admin.PostAsync($"/v1/platform/tenants/{tenantId}/close", null, Cancellation));
        await _api.WaitingForMessagesAsync(() => admin.PostAsync($"/v1/platform/tenants/{tenantId}/close", null, Cancellation));

        _api.Identity.Withdrawn.Where(withdrawn => withdrawn == first || withdrawn == second).ShouldBe([first, second], ignoreOrder: true);
    }

    // The withdrawal at the end of the invitation's life is scheduled when the link goes out; here it arrives, once the lifetime is over.
    [Fact]
    public async Task ExpireSignUp_LifetimeOver_WithdrawsTheClerkInvitationOnce()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var (invitationId, signUp) = await InviteAsync(tenantId, owner);
        _time.Advance(TimeSpan.FromDays(7));

        await ExpireAsync(tenantId, invitationId);

        _api.Identity.Withdrawn.Where(withdrawn => withdrawn == signUp).ShouldHaveSingleItem();
    }

    // The messaging tool schedules by its own clock; a message that arrives before the module's clock ends the lifetime withdraws nothing.
    [Fact]
    public async Task ExpireSignUp_BeforeTheLifetimeIsOver_WithdrawsNothing()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var (invitationId, signUp) = await InviteAsync(tenantId, owner);

        await ExpireAsync(tenantId, invitationId);

        _api.Identity.Withdrawn.ShouldNotContain(signUp);
    }

    // An accepted invitation needs no withdrawal: the person signed up with it, and our invitation is used up.
    [Fact]
    public async Task AcceptInvitation_WithAClerkInvitation_WithdrawsNothing()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var email = NewEmail();
        var (invitationId, signUp) = await InviteAsync(tenantId, owner, email);
        var invitee = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(invitee, email);
        var accepted = await _api.CreateClient(invitee).PostAsJsonAsync(
            "/v1/me/invitations/accept", new { code = _api.Email.CodeSentTo(email), firstName = "Ada", lastName = "Lovelace" }, Cancellation);
        _time.Advance(TimeSpan.FromDays(7));

        await ExpireAsync(tenantId, invitationId);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        _api.Identity.Withdrawn.ShouldNotContain(signUp);
    }

    // A call to Clerk that fails is tried again, with the retries of the invitation email; the invitation is not lost, and one email
    // goes out, with the link of the one invitation Clerk created.
    [Fact]
    public async Task InviteMember_ClerkFailsTwice_IsTriedAgainAndEmailsOnce()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var email = NewEmail();
        _api.Identity.FailNext(2);

        var invited = await _api.WaitingForMessagesAsync(() => owner.InviteMemberAsync(tenantId, email));

        invited.StatusCode.ShouldBe(HttpStatusCode.Created);
        var signUp = _api.Identity.SignUps.Where(signUp => signUp.Email == email).ShouldHaveSingleItem();
        _api.Email.Sent.Where(sent => sent.To == email).ShouldHaveSingleItem();
        _api.Email.LinkSentTo(email).ShouldBe(FakeIdentityProvider.ClerkLink(signUp.InvitationId, signUp.AcceptLink));
    }

    // When Clerk stays down, the call goes to the dead letter queue after its last try. No email goes out, so nobody holds a link that
    // leads nowhere; the invitation stays pending, and the inviter can invite again, which replaces it (K2).
    [Fact]
    public async Task InviteMember_ClerkKeepsFailing_SendsNoEmailAndKeepsTheInvitationPending()
    {
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var email = NewEmail();
        _api.Identity.IsDown = true;

        HttpResponseMessage invited = null!;
        var messages = await _api.TrackMessagesAsync(async () => invited = await owner.InviteMemberAsync(tenantId, email));

        var invitationId = (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        messages.MovedToErrorQueue.SingleMessage<OpenMemberSignUp>().InvitationId.ShouldBe(invitationId);
        _api.Email.Attempts.ShouldNotContain(attempt => attempt.Email.To == email);
        (await _catalog.InvitationStatusOfAsync(invitationId)).ShouldBe("Pending");
    }

    private async Task<(Guid TenantId, HttpClient Owner)> TenantWithOwnerAsync()
    {
        var tenant = await _catalog.AddTenantAsync();
        return (tenant.Id, _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner")));
    }

    // An invitation to a person without an account, with its email sent: its id and the id of Clerk's invitation.
    private async Task<(Guid InvitationId, string SignUp)> InviteAsync(Guid tenantId, HttpClient owner, string? email = null)
    {
        email ??= NewEmail();
        var invited = await _api.WaitingForMessagesAsync(() => owner.InviteMemberAsync(tenantId, email));
        var invitationId = (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        return (invitationId, (await SignUpInvitationIdAsync(invitationId))!);
    }

    private Task<bool> ExpireAsync(Guid tenantId, Guid invitationId) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenantId.ToString(), new ExpireSignUp(tenantId, invitationId), Cancellation);
            return true;
        });

    private Task<string?> SignUpInvitationIdAsync(Guid invitationId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT sign_up_invitation_id FROM control_plane.invitations WHERE id = '{invitationId}'");

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";
}
