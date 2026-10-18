using ControlPlane.Application.Members;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Invitations.GetInvitationPreview;

// An unknown code answers as accepting does, so a code cannot be probed. A caller without the invited address gets the acceptance's own
// refusal, and nothing of what the invitation's query read, before the inviter is looked up, also for an invitation that is cancelled,
// used or expired: a forwarded link tells its holder only what the email told them (the maintainer's decision in step 20l).
public static class GetInvitationPreviewHandler
{
    public static async Task<Result<InvitationPreviewView>> HandleAsync(
        GetInvitationPreviewQuery query,
        ITenantQueries tenants,
        IInvitationQueries invitations,
        IUserQueries users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantAsync(cancellationToken) is not { } tenant
            || await invitations.FindInvitationAsync(InvitationSecret.Hash(query.Secret), time.GetUtcNow(), cancellationToken) is not { } invitation)
        {
            return Errors.InvitationNotFound;
        }

        var allowed = Invitation.CheckEmail(invitation.Email, query.VerifiedEmails);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var invitedBy = await users.FindInviterNameAsync(invitation.InvitedBy, cancellationToken)
            ?? throw new InvalidOperationException("An invitation's inviter is always a catalog user.");
        return new InvitationPreviewView(tenant.Name, invitedBy, invitation.Role, invitation.ExpiresAt, invitation.State);
    }
}
