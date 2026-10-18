using ControlPlane.Application.Invitations.OpenMemberSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Ports;
using ControlPlane.Contracts.Invitations;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Invitations.InviteOwner;

// K6: an invitation belongs to the tenant aggregate, so the tenant row is locked first, and the Owners are counted under the lock. An
// acceptance locks the same row first, so it runs before, and the tenant has its Owner, or after, and finds its invitation cancelled. At
// most one platform Owner invitation is pending: in a tenant without an Owner nobody else invites, so every pending invitation is the
// platform's, and the new one replaces it, also to another address (the maintainer's decision in step 20k). The replaced one is saved as
// cancelled first, so the partial unique index never sees two pending invitations. Every rejection returns before the handler changes
// anything, because Wolverine commits a failed Result too (W7). The invitation then takes the path of a member's: the identity provider's
// sign-up, then the email (OpenMemberSignUpCommand). The owner's messages of the tenant creation answer its saga, which has ended (S13).
public static class InviteOwnerHandler
{
    public static async Task<(Result<InvitationView>, OwnerInvitedEvent?, OpenMemberSignUpCommand?, OutgoingMessages)> HandleAsync(
        InviteOwnerCommand command,
        ITenantCatalog catalog,
        InvitationOptions invitations,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, null, null, []);
        }

        var allowed = tenant.CheckOwnerInvitation(await catalog.CountOwnersAsync(cancellationToken));
        if (!allowed.IsSuccess)
        {
            return (allowed.Error, null, null, []);
        }

        var actor = await catalog.FindUserAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a catalog user.");

        var withdrawals = new OutgoingMessages();
        var replaced = await catalog.FindPendingInvitationsAsync(cancellationToken);
        foreach (var pending in replaced)
        {
            pending.Cancel();
            if (pending.SignUpInvitationId is { } signUp)
            {
                withdrawals.Add(new WithdrawSignUpCommand(signUp));
            }
        }

        await catalog.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        var secret = InvitationSecret.Generate();
        var invitation = Invitation.Create(
            Guid.CreateVersion7(now),
            tenant.Id,
            command.Email,
            BuiltInRoles.Owner,
            actor.Id,
            now,
            invitations.Lifetime,
            secret,
            command.Language ?? actor.Language);
        catalog.Add(invitation);
        tenant.Summarize(await catalog.ReadRowsAsync(cancellationToken));
        await catalog.SaveChangesAsync(cancellationToken);

        return (
            InvitationView.Of(invitation, actor, now),
            new OwnerInvitedEvent(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, invitation.Id, actor.Id, replaced.SingleOrDefault()?.Id),
            new OpenMemberSignUpCommand(
                tenant.Id,
                invitation.Id,
                invitation.Email,
                tenant.Name,
                invitations.AcceptLink(new InvitationCode(tenant.Id, secret)),
                invitation.ExpiresAt),
            withdrawals);
    }
}
