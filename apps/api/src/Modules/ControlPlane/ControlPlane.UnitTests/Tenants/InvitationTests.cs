using ControlPlane.Domain;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.UnitTests.Tenants;

public sealed class InvitationTests
{
    private static readonly Guid Id = new("0199a8f0-0000-7000-8000-000000000401");
    private static readonly Guid TenantId = new("0199a8f0-0000-7000-8000-000000000201");
    private static readonly Guid ActorId = new("0199a8f0-0000-7000-8000-000000000301");
    private static readonly Guid AccepterId = new("0199a8f0-0000-7000-8000-000000000302");
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private const string Token = "the-invitation-token";

    // The secret is born with the invitation, and only its hash is kept.
    [Fact]
    public void CreateInvitation_WithItsSecret_StoresTheHashNeverTheSecret()
    {
        var invitation = Invite("ada@example.com");

        invitation.SecretHash.ShouldBe(InvitationSecret.Hash(Token));
        invitation.SecretHash.ShouldNotBeNull().ShouldNotContain(Token);
    }

    // Decision 5 of step 20j: the invitation keeps the language of its email, and the person who accepts it starts with it.
    [Fact]
    public void CreateInvitation_InArabic_KeepsTheLanguage()
    {
        var invitation = Invitation.Create(Id, TenantId, "ada@example.com", BuiltInRoles.Member, ActorId, Now, Lifetime, Token, Language.Ar);

        invitation.Language.ShouldBe(Language.Ar);
    }

    [Fact]
    public void CancelInvitation_Pending_IsCancelled()
    {
        var invitation = Invite("ada@example.com");

        var cancelled = invitation.Cancel();

        cancelled.ShouldBe(StateChange.Changed);
        invitation.Status.ShouldBe(InvitationStatus.Cancelled);
    }

    // A compensation may arrive twice.
    [Fact]
    public void CancelInvitation_AlreadyCancelled_ChangesNothing()
    {
        var invitation = Invite("ada@example.com");
        invitation.Cancel();

        var cancelled = invitation.Cancel();

        cancelled.ShouldBe(StateChange.Unchanged);
        invitation.Status.ShouldBe(InvitationStatus.Cancelled);
    }

    [Fact]
    public void CancelInvitation_Accepted_StaysAccepted()
    {
        var invitation = Invite("ada@example.com");
        invitation.Accept(["ada@example.com"], AccepterId, Now);

        var cancelled = invitation.Cancel();

        cancelled.ShouldBe(StateChange.Unchanged);
        invitation.Status.ShouldBe(InvitationStatus.Accepted);
    }

    [Fact]
    public void RecordSignUp_PendingInvitation_KeepsTheProviderInvitationAndSendsTheLink()
    {
        var invitation = Invite("ada@example.com");

        var decision = invitation.RecordSignUp("inv_1");

        decision.ShouldBe(SignUpDecision.Send);
        invitation.SignUpInvitationId.ShouldBe("inv_1");
    }

    // An address with an account needs no provider invitation; the accept link goes out.
    [Fact]
    public void RecordSignUp_AddressWithAnAccount_SendsTheLink()
    {
        var invitation = Invite("ada@example.com");

        var decision = invitation.RecordSignUp(null);

        decision.ShouldBe(SignUpDecision.Send);
        invitation.SignUpInvitationId.ShouldBeNull();
    }

    // The provider's answer may arrive twice; the email went out the first time.
    [Fact]
    public void RecordSignUp_SameProviderInvitationTwice_SendsOnce()
    {
        var invitation = Invite("ada@example.com");
        invitation.RecordSignUp("inv_1");

        var decision = invitation.RecordSignUp("inv_1");

        decision.ShouldBe(SignUpDecision.Ignore);
    }

    // A second provider invitation for the same invitation, from a repeated call, would let someone sign up with a link nobody has.
    [Fact]
    public void RecordSignUp_AnotherProviderInvitation_IsWithdrawn()
    {
        var invitation = Invite("ada@example.com");
        invitation.RecordSignUp("inv_1");

        var decision = invitation.RecordSignUp("inv_2");

        decision.ShouldBe(SignUpDecision.Withdraw);
        invitation.SignUpInvitationId.ShouldBe("inv_1");
    }

    // The invitation was revoked while the provider answered: no email goes out, and the provider's invitation is withdrawn.
    [Fact]
    public void RecordSignUp_CancelledInvitation_WithdrawsTheProviderInvitation()
    {
        var invitation = Invite("ada@example.com");
        invitation.Cancel();

        var decision = invitation.RecordSignUp("inv_1");

        decision.ShouldBe(SignUpDecision.Withdraw);
        invitation.SignUpInvitationId.ShouldBeNull();
    }

    [Fact]
    public void RecordSignUp_CancelledInvitationOfAnAddressWithAnAccount_SendsNothing()
    {
        var invitation = Invite("ada@example.com");
        invitation.Cancel();

        var decision = invitation.RecordSignUp(null);

        decision.ShouldBe(SignUpDecision.Ignore);
    }

    [Fact]
    public void AcceptInvitation_Cancelled_IsRefused()
    {
        var invitation = Invite("ada@example.com");
        invitation.Cancel();

        var accepted = invitation.Accept(["ada@example.com"], AccepterId, Now);

        accepted.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationNotPending);
        invitation.Status.ShouldBe(InvitationStatus.Cancelled);
    }

    [Fact]
    public void InviteOwner_NewInvitation_IsPendingUntilItsLifetimeEnds()
    {
        var invitation = Invite("ada@example.com");

        invitation.Status.ShouldBe(InvitationStatus.Pending);
        invitation.ExpiresAt.ShouldBe(new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void AcceptInvitation_VerifiedEmailMatches_IsAccepted()
    {
        var invitation = Invite("ada@example.com");

        var accepted = invitation.Accept(["other@example.com", "ada@example.com"], AccepterId, Now.AddDays(1));

        accepted.IsSuccess.ShouldBeTrue();
        invitation.Status.ShouldBe(InvitationStatus.Accepted);
        invitation.AcceptedBy.ShouldBe(AccepterId);
    }

    [Fact]
    public void AcceptInvitation_EmailInAnotherCase_IsAccepted()
    {
        var invitation = Invite("Ada@Example.com");

        var accepted = invitation.Accept(["ada@example.com"], AccepterId, Now);

        accepted.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void AcceptInvitation_AlreadyAccepted_IsRejected()
    {
        var invitation = Invite("ada@example.com");
        invitation.Accept(["ada@example.com"], AccepterId, Now);

        var second = invitation.Accept(["ada@example.com"], AccepterId, Now);

        second.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationNotPending);
    }

    [Fact]
    public void AcceptInvitation_AfterItsLifetime_IsRejectedAsExpired()
    {
        var invitation = Invite("ada@example.com");

        var accepted = invitation.Accept(["ada@example.com"], AccepterId, Now.AddDays(7));

        accepted.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationExpired);
        invitation.Status.ShouldBe(InvitationStatus.Pending);
    }

    // The token alone is not enough: a forwarded link does not let someone else in.
    [Fact]
    public void AcceptInvitation_WithoutTheInvitedEmail_IsRejected()
    {
        var invitation = Invite("ada@example.com");

        var accepted = invitation.Accept(["grace@example.com"], AccepterId, Now);

        accepted.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationEmailMismatch);
        invitation.Status.ShouldBe(InvitationStatus.Pending);
    }

    // The checks of an acceptance, apart from the change, so a handler can refuse after them and still change nothing (W7).
    [Fact]
    public void CheckAcceptance_Allowed_ChangesNothing()
    {
        var invitation = Invite("ada@example.com");

        var allowed = invitation.CheckAcceptance(["ada@example.com"], Now);

        allowed.IsSuccess.ShouldBeTrue();
        invitation.Status.ShouldBe(InvitationStatus.Pending);
        invitation.AcceptedBy.ShouldBeNull();
    }

    [Fact]
    public void CheckAcceptance_WithoutTheInvitedEmail_IsRefused()
    {
        var invitation = Invite("ada@example.com");

        var allowed = invitation.CheckAcceptance(["grace@example.com"], Now);

        allowed.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationEmailMismatch);
        invitation.Status.ShouldBe(InvitationStatus.Pending);
    }

    // The accept screen says which: a pending invitation is expired from the moment of its expiry, read when it is asked, never stored.
    [Fact]
    public void PreviewInvitation_PendingBeforeItsExpiry_IsPending()
    {
        var invitation = Invite("ada@example.com");

        invitation.StateAt(Now.AddDays(7).AddTicks(-1)).ShouldBe(InvitationState.Pending);
    }

    [Fact]
    public void PreviewInvitation_PendingAtItsExpiry_IsExpired()
    {
        var invitation = Invite("ada@example.com");

        invitation.StateAt(Now.AddDays(7)).ShouldBe(InvitationState.Expired);
    }

    [Fact]
    public void PreviewInvitation_AcceptedAfterItsExpiry_IsAccepted()
    {
        var invitation = Invite("ada@example.com");
        invitation.Accept(["ada@example.com"], AccepterId, Now);

        invitation.StateAt(Now.AddDays(8)).ShouldBe(InvitationState.Accepted);
    }

    [Fact]
    public void PreviewInvitation_CancelledAfterItsExpiry_IsCancelled()
    {
        var invitation = Invite("ada@example.com");
        invitation.Cancel();

        invitation.StateAt(Now.AddDays(8)).ShouldBe(InvitationState.Cancelled);
    }

    // The preview refuses a caller without the invited address before it reads the state, with the error the acceptance gives.
    [Fact]
    public void PreviewInvitation_CancelledWithoutTheInvitedEmail_IsRefusedAsTheAcceptanceIs()
    {
        var invitation = Invite("ada@example.com");
        invitation.Cancel();

        var allowed = invitation.CheckEmail(["grace@example.com"]);

        allowed.Error.ShouldBe(Invite("ada@example.com").CheckAcceptance(["grace@example.com"], Now).Error);
    }

    [Fact]
    public void PreviewInvitation_TheInvitedEmailInAnotherCase_IsAllowed()
    {
        var invitation = Invite("Ada@Example.com");

        invitation.CheckEmail(["other@example.com", " ada@example.com "]).IsSuccess.ShouldBeTrue();
    }

    // K1: only an Owner gives the Owner role, so only an Owner invites an Owner.
    [Fact]
    public void InviteMember_AnAdminForTheOwnerRole_IsForbidden()
    {
        var allowed = Invitation.CheckInviter(BuiltInRoles.Admin, BuiltInRoles.Owner);

        allowed.Error.Code.ShouldBe(ControlPlaneErrorCodes.InvitationOwnerRequiresOwner);
        allowed.Error.Type.ShouldBe(SharedKernel.ErrorType.Forbidden);
    }

    [Theory]
    [InlineData("Owner", "Owner")]
    [InlineData("Owner", "Admin")]
    [InlineData("Owner", "Member")]
    [InlineData("Admin", "Admin")]
    [InlineData("Admin", "Member")]
    public void InviteMember_ARoleTheInviterMayGive_IsAllowed(string inviter, string invited)
    {
        var allowed = Invitation.CheckInviter(RoleOf(inviter), RoleOf(invited));

        allowed.IsSuccess.ShouldBeTrue();
    }

    private static Role RoleOf(string role) => BuiltInRoles.All.Single(builtIn => builtIn.BuiltIn.ToString() == role);

    private static Invitation Invite(string email) =>
        Invitation.Create(Id, TenantId, email, BuiltInRoles.Member, ActorId, Now, Lifetime, Token, Language.En);
}
