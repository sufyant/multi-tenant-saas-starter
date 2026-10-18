using ControlPlane.Application.Invitations;
using ControlPlane.Application.Tenants.ActivateTenant;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Application.Users;
using ControlPlane.Domain;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine;
using Wolverine.Persistence;

namespace ControlPlane.Application.Tenants.CreateTenant;

// Step 1 of the tenant creation saga, in one transaction: the tenant as provisioning, its first owner's invitation, the saga's record
// (section 6). The host also saves the command result in this transaction (I4). The invitation's secret is born here; only its hash is
// stored, and the accept link that carries it goes on in the activation's message. Rejections come first (W7); a taken slug is found by
// the insert itself, which writes nothing then.
public static class CreateTenantHandler
{
    public static async Task<(Result<TenantView>, Insert<TenantCreationSaga>?, DeliveryMessage<ActivateTenantCommand>?, ActivationTimedOut?)> HandleAsync(
        CreateTenantCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        InvitationOptions invitations,
        TenantCreationOptions tenantCreation,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var actor = await users.FindActorAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");

        var now = time.GetUtcNow();
        var tenant = Tenant.Create(tenants.TenantId, command.Name, command.Slug, now);
        if (!tenant.IsSuccess)
        {
            return (tenant.Error, null, null, null);
        }

        if (!await tenants.TryAddAsync(tenant.Value, cancellationToken))
        {
            return (Error.Conflict(ControlPlaneErrorCodes.TenantSlugTaken, "Another tenant already has this slug."), null, null, null);
        }

        var secret = InvitationSecret.Generate();
        var invitation = Invitation.Create(
            Guid.CreateVersion7(now),
            tenant.Value.Id,
            command.OwnerEmail,
            BuiltInRoles.Owner,
            actor.Id,
            now,
            invitations.Lifetime,
            secret,
            command.Language ?? actor.Language);
        tenants.Add(invitation);

        // R12: the new tenant's summary names its Owner invitation. The tenant was inserted by SQL, so it is read back, locked, to change it.
        var added = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant was added in this transaction.");
        added.Summarize(await tenants.ReadSummaryAsync(cancellationToken));

        var (saga, activate, timeout) = TenantCreationSaga.Begin(
            tenant.Value.Id,
            invitation.Id,
            invitations.AcceptLink(new InvitationCode(tenant.Value.Id, secret)),
            tenantCreation.Timeouts);

        // A saga that is only being started gives its messages no saga id of their own; this one needs it, so that its fault finds
        // the saga.
        return (
            TenantView.Of(tenant.Value),
            Storage.Insert(saga),
            activate.WithDeliveryOptions(new DeliveryOptions { SagaId = saga.Id.ToString() }),
            timeout);
    }
}
