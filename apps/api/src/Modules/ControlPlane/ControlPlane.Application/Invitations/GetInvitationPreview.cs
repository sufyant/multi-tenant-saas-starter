using ControlPlane.Application.Members;
using ControlPlane.Application.Ports;
using ControlPlane.Application.Users;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Application.Invitations;

/// <summary>
/// What the accept screen shows before the person accepts: the invitation the secret names, in the tenant its code names, as accepting
/// finds it (section 6). The signed-in user's verified email addresses are read from the identity provider before the transaction
/// begins. It reads only: it changes nothing and publishes nothing, so no audit record is written (K3).
/// </summary>
public sealed record GetInvitationPreviewQuery(string Secret, IReadOnlyList<string> VerifiedEmails);

/// <summary>
/// Who invited, as the accept screen names them: a user, by their names as the catalog holds them now, or the platform, when a system
/// admin invited (<c>User</c>, <c>Platform</c>). The screen never sees a system admin's name.
/// </summary>
public sealed record InviterNameView(string Kind, UserNameView? Name);

/// <summary>
/// The tenant's name, the inviter, the role the invitation gives, its expiry, and its state at the
/// moment of the request (<c>Pending</c>, <c>Expired</c>, <c>Accepted</c>, <c>Cancelled</c>).
/// </summary>
public sealed record InvitationPreviewView(string TenantName, InviterNameView Inviter, RoleView Role, DateTimeOffset ExpiresAt, string State);

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
            || await catalog.FindInvitationAsync(InvitationToken.Hash(query.Secret), cancellationToken) is not { } invitation)
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
