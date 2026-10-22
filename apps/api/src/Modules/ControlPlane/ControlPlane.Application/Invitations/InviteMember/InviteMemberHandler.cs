using ControlPlane.Application.Invitations.OpenSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Members;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Invitations.InviteMember;

// K6: an invitation belongs to the tenant aggregate, so the tenant row is locked first; two invitations at once then run one after the
// other, and neither passes the daily limit nor leaves a second pending invitation. An inviter who is no longer a member, and a tenant
// that is no longer active, are refused first (TenantActor); the rules of the invitation are the tenant's (M1, Tenant.Invite). Rejections
// come first (W7). The secret is born here; only its hash is stored, and the link that carries it goes on to the identity provider's
// sign-up, after the commit. A replaced invitation's sign-up is withdrawn.
public static class InviteMemberHandler
{
    public static async Task<(Result<InvitationView>, OutgoingMessages)> HandleAsync(
        InviteMemberCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        InvitationOptions invitations,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var start = await TenantActor.LockAsync(tenants, users, command.ActorId, cancellationToken);
        if (!start.IsSuccess)
        {
            return (start.Error, []);
        }

        var (tenant, actor, actorMembership) = start.Value;

        var now = time.GetUtcNow();
        var email = EmailAddress.Create(command.Email);
        var roles = await tenants.FindRolesAsync(cancellationToken);
        if (roles.Find(command.RoleId) is not { } role)
        {
            return (Role.NotFound, []);
        }

        var inviter = Inviter.Member(actor.Id, actor.Language, roles.Of(actorMembership.RoleId));
        var secret = InvitationSecret.Generate();
        var invited = tenant.Invite(
            inviter,
            new NewInvitation(Guid.CreateVersion7(now), email, role, command.Language, secret, now, invitations.Lifetime),
            await tenants.FindInvitationFactsAsync(email, now - Tenant.InvitationLimitWindow, cancellationToken),
            invitations.DailyLimitPerTenant);
        if (!invited.IsSuccess)
        {
            return (invited.Error, []);
        }

        var (invitation, replaced) = invited.Value;
        await tenants.AddInvitationAsync(tenant, invited.Value, cancellationToken);

        return (
            InvitationView.Of(invitation, role, UserNameView.Of(actor), now),
            [
                new MemberInvitedEvent(
                    Guid.CreateVersion7(now),
                    now,
                    tenant.Id,
                    invitation.Id,
                    actor.Id,
                    role.Name,
                    replaced.SingleOrDefault()?.Id,
                    role.Id,
                    role.BuiltIn is not null),
                OpenSignUpCommand.For(tenant, invitation, invitations.AcceptLink(new InvitationCode(tenant.Id, secret))),
                .. WithdrawSignUpCommand.For(replaced),
            ]);
    }
}
