using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.Invitations;
using Wolverine;

namespace ControlPlane.Application.Invitations.SignUp;

// Records the identity provider's answer on our invitation, and only then hands the link to Notifications, so no email carries a link
// to an invitation that was revoked, replaced or closed meanwhile. The invitation belongs to the tenant aggregate, so the tenant row is
// locked first (K6): a revocation or a closure runs before or after, never between the check and the change. The withdrawal at the end
// of the invitation's life is scheduled in the same transaction.
public static class RecordSignUpHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        SignUpOpened opened,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        _ = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("An invitation's sign-up runs in the tenant of the invitation.");
        var invitation = await catalog.FindInvitationForUpdateAsync(opened.InvitationId, cancellationToken)
            ?? throw new InvalidOperationException("An invitation's sign-up follows the invitation.");

        var decision = invitation.RecordSignUp(opened.SignUpInvitationId);
        if (decision == SignUpDecision.Withdraw)
        {
            return [new WithdrawSignUp(opened.SignUpInvitationId!)];
        }

        if (decision == SignUpDecision.Ignore)
        {
            return [];
        }

        await catalog.SaveChangesAsync(cancellationToken);

        // The email speaks the invitation's language and says how long its link works.
        var now = time.GetUtcNow();
        var (language, lifetime) = (invitation.Language, invitation.ExpiresAt - invitation.CreatedAt);
        OutgoingMessages messages =
        [
            opened.ForFirstOwner
                ? new OwnerInvitationReady(
                    Guid.CreateVersion7(now), now, opened.TenantId, opened.InvitationId, opened.Email, opened.TenantName, opened.Link, language, lifetime)
                : new MemberInvitationReady(
                    Guid.CreateVersion7(now), now, opened.TenantId, opened.InvitationId, opened.Email, opened.TenantName, opened.Link, language, lifetime),
        ];
        if (opened.SignUpInvitationId is not null)
        {
            messages.Add(new ExpireSignUp(opened.TenantId, opened.InvitationId).ScheduledAt(opened.ExpiresAt));
        }

        return messages;
    }
}

// The invitation's lifetime is over. An invitation still pending keeps its identity provider's invitation until now, so it is withdrawn;
// a revoked, replaced or cancelled one withdrew it when it ended, and an accepted one no longer needs it. The message is scheduled by the
// messaging tool's clock; the module's clock decides whether the lifetime is over, so a message that arrives early withdraws nothing.
public static class ExpireSignUpHandler
{
    public static async Task<WithdrawSignUp?> HandleAsync(
        ExpireSignUp expire,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        _ = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("An invitation's sign-up runs in the tenant of the invitation.");
        var invitation = await catalog.FindInvitationForUpdateAsync(expire.InvitationId, cancellationToken);
        return invitation is { Status: InvitationStatus.Pending, SignUpInvitationId: { } signUp } && time.GetUtcNow() >= invitation.ExpiresAt
            ? new WithdrawSignUp(signUp)
            : null;
    }
}
