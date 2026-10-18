using ControlPlane.Application.Members;
using ControlPlane.Application.Ports;
using ControlPlane.Application.Users;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Invitations.GetInvitationPreview;

// An unknown code answers as accepting does, so a code cannot be probed. A caller without the invited address gets the acceptance's own
// refusal before anything else is read, also for an invitation that is cancelled, used or expired: a forwarded link tells its holder only
// what the email told them (the maintainer's decision in step 20l).
public static class GetInvitationPreviewHandler
{
    public static async Task<Result<InvitationPreviewView>> HandleAsync(
        GetInvitationPreviewQuery query,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantAsync(cancellationToken) is not { } tenant
            || await catalog.FindInvitationAsync(InvitationSecret.Hash(query.Secret), cancellationToken) is not { } invitation)
        {
            return Errors.InvitationNotFound;
        }

        var allowed = invitation.CheckEmail(query.VerifiedEmails);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var inviter = await catalog.FindUserAsync(invitation.InvitedBy, cancellationToken)
            ?? throw new InvalidOperationException("An invitation's inviter is always a catalog user.");
        var invitedBy = await catalog.IsSystemAdminAsync(inviter.Id, cancellationToken)
            ? new InviterNameView("Platform", null)
            : new InviterNameView("User", UserNameView.Of(inviter));
        return new InvitationPreviewView(
            tenant.Name,
            invitedBy,
            RoleView.Of(BuiltInRoles.All.Single(role => role.Id == invitation.RoleId)),
            invitation.ExpiresAt,
            invitation.StateAt(time.GetUtcNow()).ToString());
    }
}
