using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Members;
using ControlPlane.Domain.Roles;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Members.MakeOwner;

// K6: a membership belongs to the tenant aggregate, so the tenant row is locked first, and the member's facts are read under the lock
// (M1). The user's status is a value, read without a lock of the user (K6a), so the lock order stays as it is. The rule is the tenant's
// (Tenant.MakeOwner), and the handler finds the Owner role it gives by its kind, as InviteOwner does (A1). Rejections come first (W7). It saves through SaveAsync, so the summary counts the new Owner in the same
// transaction (R12). The platform acted: OwnerMadeEvent gives the platform's log tenant.owner_made, by the system admin, and the
// tenant's log member.role_changed, by the platform (K3).
public static class MakeOwnerHandler
{
    public static async Task<(Result<UserMembershipView>, OutgoingMessages)> HandleAsync(
        MakeOwnerCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, []);
        }

        var facts = await tenants.FindMemberFactsAsync(command.UserId, cancellationToken);
        var owner = facts.Roles.BuiltIn(BuiltInRole.Owner);
        var made = tenant.MakeOwner(owner, facts, await users.FindStatusAsync(command.UserId, cancellationToken));
        if (!made.IsSuccess)
        {
            return (made.Error, []);
        }

        var (target, oldRole, _) = made.Value;
        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        await tenants.SaveAsync(tenant, cancellationToken);

        var now = time.GetUtcNow();
        return (
            new UserMembershipView(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString(), RoleView.Of(owner), target.JoinedAt, LastOwner: facts.Owners == 0),
            [
                new OwnerMadeEvent(
                    Guid.CreateVersion7(now),
                    now,
                    tenant.Id,
                    tenant.Name,
                    target.UserId,
                    actor,
                    oldRole.Name,
                    oldRole.Id,
                    oldRole.BuiltIn is not null,
                    owner.Name,
                    owner.Id),
            ]);
    }
}
