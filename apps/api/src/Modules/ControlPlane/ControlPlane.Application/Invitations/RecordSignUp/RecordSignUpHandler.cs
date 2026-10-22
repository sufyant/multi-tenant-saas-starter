using ControlPlane.Application.Invitations.ExpireSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Contracts.Invitations;
using ControlPlane.Domain.Tenants;
using Wolverine;

namespace ControlPlane.Application.Invitations.RecordSignUp;

// Records the identity provider's answer on our invitation, and only then hands the link to Notifications, so no email carries a link
// to an invitation that was revoked, replaced or closed meanwhile. The invitation belongs to the tenant aggregate, so the tenant row is
// locked first (K6m): a revocation or a closure runs before or after, never between the check and the change. The withdrawal at the end
// of the invitation's life is scheduled in the same transaction.
public static class RecordSignUpHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        RecordSignUpCommand opened,
        ITenantRepository tenants,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("An invitation's sign-up runs in the tenant of the invitation.");
        var invitation = await tenants.FindInvitationForUpdateAsync(opened.InvitationId, cancellationToken)
            ?? throw new InvalidOperationException("An invitation's sign-up follows the invitation.");

        var decision = invitation.RecordSignUp(opened.SignUpInvitationId);
        if (decision == SignUpDecision.Withdraw)
        {
            return [new WithdrawSignUpCommand(opened.SignUpInvitationId!)];
        }

        if (decision == SignUpDecision.Ignore)
        {
            return [];
        }

        await tenants.SaveAsync(tenant, cancellationToken);

        // The email speaks the invitation's language and says how long its link works.
        var now = time.GetUtcNow();
        var (language, lifetime) = (invitation.Language, invitation.ExpiresAt - invitation.CreatedAt);
        OutgoingMessages messages =
        [
            opened.ForFirstOwner
                ? new OwnerInvitationReadyEvent(
                    Guid.CreateVersion7(now), now, opened.TenantId, opened.InvitationId, opened.Email, opened.TenantName, opened.Link, language, lifetime)
                : new MemberInvitationReadyEvent(
                    Guid.CreateVersion7(now), now, opened.TenantId, opened.InvitationId, opened.Email, opened.TenantName, opened.Link, language, lifetime),
        ];
        if (opened.SignUpInvitationId is not null)
        {
            messages.Add(new ExpireSignUpCommand(opened.TenantId, opened.InvitationId).ScheduledAt(opened.ExpiresAt));
        }

        return messages;
    }
}
