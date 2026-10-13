using ControlPlane.Application.Invitations;
using ControlPlane.Application.Ports;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using FluentValidation;
using SharedKernel;
using Wolverine;
using Wolverine.Persistence;

namespace ControlPlane.Application.Tenants.TenantCreation;

/// <summary>
/// A system admin creates a tenant. It runs inside the tenant it creates, whose id the caller chose, so every step
/// of the saga runs in that tenant's transaction and hands the tenant on to the next one. The host's idempotency layer protects
/// the HTTP request (section 3). The command carries no request key.
/// </summary>
public sealed record CreateTenant(string AdminId, string Name, string Slug, string OwnerEmail);

public sealed record TenantDetails(Guid Id, string Name, string Slug, string Status);

public sealed class CreateTenantValidator : AbstractValidator<CreateTenant>
{
    public CreateTenantValidator()
    {
        // The tenant checks the name and the slug itself; a request without one never reaches it.
        RuleFor(command => command.Name).NotNull();
        RuleFor(command => command.Slug).NotNull();
        RuleFor(command => command.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(Invitation.EmailMaxLength);
    }
}

// Step 1 of the tenant creation saga, in one transaction: the tenant as provisioning, its first owner's invitation, the saga's record
// (section 6). The host also saves the command result in this transaction (I4). The invitation's secret is born here; only its hash is stored,
// and the accept link that carries it goes on in the activation's message. Every rejection returns before the handler changes
// anything, because Wolverine commits a failed Result too (W7); a taken slug is found by the insert itself, which writes nothing then.
public static class CreateTenantHandler
{
    public static async Task<(Result<TenantDetails>, Insert<TenantCreationSaga>?, DeliveryMessage<ActivateTenant>?, ActivationTimedOut?)> HandleAsync(
        CreateTenant command,
        ITenantCatalog catalog,
        InvitationSettings invitations,
        TenantCreationSettings tenantCreation,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var admin = await catalog.FindUserAsync(command.AdminId, cancellationToken)
            ?? throw new InvalidOperationException("A system admin is always a catalog user.");

        var tenant = Tenant.Create(catalog.TenantId, command.Name, command.Slug);
        if (!tenant.IsSuccess)
        {
            return (tenant.Error, null, null, null);
        }

        if (!await catalog.TryAddAsync(tenant.Value, cancellationToken))
        {
            return (Error.Conflict("tenant.slug_taken", "Another tenant already has this slug."), null, null, null);
        }

        var now = time.GetUtcNow();
        var secret = InvitationToken.Generate();
        var invitation = Invitation.Create(
            Guid.CreateVersion7(now), tenant.Value.Id, command.OwnerEmail, BuiltInRoles.Owner, admin.Id, now, invitations.Lifetime, secret);
        catalog.Add(invitation);

        var (saga, activate, timeout) = TenantCreationSaga.Begin(
            tenant.Value.Id,
            invitation.Id,
            invitations.AcceptLink(new InvitationCode(tenant.Value.Id, secret)),
            tenantCreation.Timeouts);

        // A saga that is only being started gives its messages no saga id of their own; this one needs it, so that its fault finds
        // the saga.
        return (
            Details(tenant.Value),
            Storage.Insert(saga),
            activate.WithDeliveryOptions(new DeliveryOptions { SagaId = saga.Id.ToString() }),
            timeout);
    }

    private static TenantDetails Details(Tenant tenant) => new(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString());
}
