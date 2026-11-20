using ControlPlane.Application.Plans;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Domain;
using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.CreateMyTenant;

// One transaction writes the active tenant, its summary, its roles and its creator's Owner membership (docs/flows/tenant-creation.md,
// section 2). The user row is locked first, so two creations of one person run one after the other and the limit counts each (K6f); the
// tenant is new, so no other transaction waits for its row. The host saves the command result in this transaction (I4). Rejections come
// first (W7); a taken slug is found by the insert itself, which writes nothing then.
public static class CreateMyTenantHandler
{
    public static async Task<(Result<MyTenantView>, OutgoingMessages)> HandleAsync(
        CreateMyTenantCommand command,
        SignUpOptions signUp,
        ITenantRepository tenants,
        IUserRepository users,
        IPlanRepository plans,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (!signUp.Enabled)
        {
            return (Errors.SignUpDisabled, []);
        }

        if (await users.FindUserForNoKeyUpdateAsync(command.ActorId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, []);
        }

        // A8: a deletion that committed after the door let the user in is refused under the lock, so a deleted user never becomes an Owner.
        if (user.DeletedAt is not null)
        {
            return (Error.Forbidden(ControlPlaneErrorCodes.UserDeleted, "Your account is deleted."), []);
        }

        if (await users.CountCreatedTenantsAsync(user.Id, cancellationToken) >= signUp.TenantLimit)
        {
            return (Error.Conflict(ControlPlaneErrorCodes.TenantCreationLimitReached, "The person created as many tenants as one person may."), []);
        }

        var planId = await plans.FindDefaultPlanIdAsync(cancellationToken);
        var plan = await plans.FindPlanNameAsync(planId, cancellationToken) ?? throw new InvalidOperationException("The default plan exists.");
        var now = time.GetUtcNow();
        var created = Tenant.Create(tenants.TenantId, command.Name, command.Slug, planId, now, user.Id);
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

        // The tenant was inserted by SQL, so it is read back, locked, to add its Owner and save its summary under the lock (K6a, R12). The
        // membership counts on the user row (R13).
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant was added in this transaction.");
        var owner = new TenantRoles(roles).BuiltIn(BuiltInRole.Owner);
        await tenants.AddMembershipAsync(tenant, user.Id, owner.Id, now, cancellationToken);

        return (
            MyTenantView.Of(tenant, owner),
            [new TenantCreatedEvent(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, user.Id, plan.Id, plan.Name, ByItsOwner: true)]);
    }
}
