using ControlPlane.Domain;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.UnitTests.Invitations;

public class InvitationTests
{
    private static readonly Guid Id = new("0199a8f0-0000-7000-8000-000000000401");
    private static readonly Guid TenantId = new("0199a8f0-0000-7000-8000-000000000201");
    private static readonly Guid InviterId = new("0199a8f0-0000-7000-8000-000000000301");
    private static readonly Guid AccepterId = new("0199a8f0-0000-7000-8000-000000000302");
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private const string Token = "the-invitation-token";

    // The secret is born with the invitation, and only its hash is kept.
    [Fact]
    public void CreateInvitation_WithItsSecret_StoresTheHashNeverTheSecret()
    {
        var invitation = Invite("ada@example.com");

        invitation.TokenHash.ShouldBe(InvitationToken.Hash(Token));
        invitation.TokenHash.ShouldNotBeNull().ShouldNotContain(Token);
    }

    [Fact]
    public void CancelInvitation_Pending_IsCancelled()
    {
        var invitation = Invite("ada@example.com");

        var cancelled = invitation.Cancel();

        cancelled.ShouldBeTrue();
        invitation.Status.ShouldBe(InvitationStatus.Cancelled);
    }

    // A compensation may arrive twice.
    [Fact]
    public void CancelInvitation_AlreadyCancelled_ChangesNothing()
    {
        var invitation = Invite("ada@example.com");
        invitation.Cancel();

        var cancelled = invitation.Cancel();

        cancelled.ShouldBeFalse();
        invitation.Status.ShouldBe(InvitationStatus.Cancelled);
    }

    [Fact]
    public void CancelInvitation_Accepted_StaysAccepted()
    {
        var invitation = Invite("ada@example.com");
        invitation.Accept(["ada@example.com"], AccepterId, Now);

        var cancelled = invitation.Cancel();

        cancelled.ShouldBeFalse();
        invitation.Status.ShouldBe(InvitationStatus.Accepted);
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

    private static Role RoleOf(string role) => BuiltInRoles.All.Single(builtIn => builtIn.Name == role);

    private static Invitation Invite(string email) =>
        Invitation.Create(Id, TenantId, email, BuiltInRoles.Member, InviterId, Now, Lifetime, Token);
}
