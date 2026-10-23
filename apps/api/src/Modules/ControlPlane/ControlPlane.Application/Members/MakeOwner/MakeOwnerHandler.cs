using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Members;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Members.MakeOwner;

// K6: a membership belongs to the tenant aggregate, so the tenant row is locked first, and the member's facts are read under the lock
// (M1). The user's status is a value, read without a lock of the user (K6a), so the lock order stays as it is. The rule is the tenant's
// (Tenant.MakeOwner). Rejections come first (W7). It saves through SaveAsync, so the summary counts the new Owner in the same
// transaction (R12). The platform acted: the tenant's log names the platform, and the platform's log the system admin (K3).
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
        var made = tenant.MakeOwner(facts, await users.FindStatusAsync(command.UserId, cancellationToken));
        if (!made.IsSuccess)
        {
            return (made.Error, []);
        }

        var (target, oldRole, _) = made.Value;
        var owner = facts.Roles.Of(target.RoleId);
        var actor = await users.FindUserIdAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A system actor is always a user.");
        await tenants.SaveAsync(tenant, cancellationToken);

        var now = time.GetUtcNow();
        return (
            new UserMembershipView(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString(), RoleView.Of(owner), target.JoinedAt, LastOwner: facts.Owners == 0),
            [
                new MemberRoleChangedEvent(
                    Guid.CreateVersion7(now),
                    now,
                    tenant.Id,
                    target.UserId,
                    actor,
                    oldRole.Name,
                    owner.Name,
                    oldRole.Id,
                    oldRole.BuiltIn is not null,
                    owner.Id,
                    NewRoleBuiltIn: true,
                    ByPlatform: true),
            ]);
    }
}
