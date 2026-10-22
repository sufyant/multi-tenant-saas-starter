using ControlPlane.Application.Invitations.OpenSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Invitations;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Invitations.InviteOwner;

// K6: an invitation belongs to the tenant aggregate, so the tenant row is locked first, and the Owners are counted under the lock. An
// acceptance locks the same row first, so it runs before, and the tenant has its Owner, or after, and finds its invitation cancelled.
// The rules are the tenant's (M1, Tenant.Invite): the platform invites an Owner only into an active tenant without one, and the new
// invitation replaces every pending one (the maintainer's decision in step 20k). Rejections come first (W7). The invitation then takes
// the path of every invitation: the identity provider's sign-up, then the email (OpenSignUpCommand). Tenant creation takes the same path.
public static class InviteOwnerHandler
{
    public static async Task<(Result<InvitationView>, OutgoingMessages)> HandleAsync(
        InviteOwnerCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        InvitationOptions invitations,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, []);
        }

        var actor = await users.FindProfileAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");

        var now = time.GetUtcNow();
        var email = EmailAddress.Create(command.Email);
        var secret = InvitationSecret.Generate();
        var invited = tenant.Invite(
            Inviter.Platform(actor.Id, actor.Language),
            new NewInvitation(Guid.CreateVersion7(now), email, BuiltInRoles.Owner, command.Language, secret, now, invitations.Lifetime),
            await tenants.FindInvitationFactsAsync(email, now - Tenant.InvitationLimitWindow, cancellationToken),
            invitations.DailyLimitPerTenant);
        if (!invited.IsSuccess)
        {
            return (invited.Error, []);
        }

        var (invitation, replaced) = invited.Value;
        await tenants.AddInvitationAsync(tenant, invited.Value, cancellationToken);

        return (
            InvitationView.Of(invitation, UserNameView.Of(actor), now),
            [
                new OwnerInvitedEvent(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, invitation.Id, actor.Id, replaced.SingleOrDefault()?.Id),
                OpenSignUpCommand.For(tenant, invitation, invitations.AcceptLink(new InvitationCode(tenant.Id, secret))),
                .. WithdrawSignUpCommand.For(replaced),
            ]);
    }
}
