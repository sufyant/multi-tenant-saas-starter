using ControlPlane.Application.Invitations;
using ControlPlane.Application.Invitations.OpenSignUp;
using ControlPlane.Application.Plans;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Domain;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.CreateTenant;

// One transaction writes the active tenant, its summary, its roles and its first Owner's invitation, and sends the identity provider's sign-up and
// the audit event through the outbox (docs/flows/tenant-creation.md). The host also saves the command result in this transaction
// (I4). The invitation takes the path of InviteOwner: the same rule (Tenant.Invite, with the platform as inviter, M1), then the sign-up
// and the email. Rejections come first (W7); a taken slug is found by the insert itself, which writes nothing then.
public static class CreateTenantHandler
{
    public static async Task<(Result<TenantView>, OutgoingMessages)> HandleAsync(
        CreateTenantCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        IPlanRepository plans,
        InvitationOptions invitations,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var actor = await users.FindProfileAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");

        var now = time.GetUtcNow();
        var created = Tenant.Create(tenants.TenantId, command.Name, command.Slug, await plans.FindDefaultPlanIdAsync(cancellationToken), now);
        if (!created.IsSuccess)
        {
            return (created.Error, []);
        }

        // A2: a tenant has its own built-in roles from its creation on, written in its transaction.
        IReadOnlyList<Role> roles = [.. Enum.GetValues<BuiltInRole>().Select(builtIn => Role.CreateBuiltIn(Guid.CreateVersion7(now), created.Value.Id, builtIn))];
        if (!await tenants.TryAddAsync(created.Value, roles, cancellationToken))
        {
            return (Error.Conflict(ControlPlaneErrorCodes.TenantSlugTaken, "Another tenant already has this slug."), []);
        }

        // The tenant was inserted by SQL, so it is read back, locked, to invite its Owner and save its summary under the lock (K6a, R12).
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant was added in this transaction.");
        var email = EmailAddress.Create(command.OwnerEmail);
        var secret = InvitationSecret.Generate();
        var invited = tenant.Invite(
            Inviter.Platform(actor.Id, actor.Language),
            new NewInvitation(Guid.CreateVersion7(now), email, new TenantRoles(roles).BuiltIn(BuiltInRole.Owner), command.Language, secret, now, invitations.Lifetime),
            await tenants.FindInvitationFactsAsync(email, now - Tenant.InvitationLimitWindow, cancellationToken),
            invitations.DailyLimitPerTenant,
            await tenants.FindEntitlementsAsync(tenant, now, cancellationToken));
        if (!invited.IsSuccess)
        {
            throw new InvalidOperationException($"A new tenant has no Owner and no invitation: {invited.Error.Code}.");
        }

        var invitation = invited.Value.Invitation;
        await tenants.AddInvitationAsync(tenant, invited.Value, cancellationToken);

        return (
            TenantView.Of(tenant),
            [
                new TenantCreatedEvent(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, actor.Id),
                OpenSignUpCommand.For(tenant, invitation, invitations.AcceptLink(new InvitationCode(tenant.Id, secret))),
            ]);
    }
}
