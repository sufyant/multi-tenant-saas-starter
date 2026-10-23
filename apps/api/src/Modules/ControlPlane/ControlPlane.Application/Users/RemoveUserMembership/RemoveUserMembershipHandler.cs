using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Contracts.Users;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Users.RemoveUserMembership;

// K6l: a membership belongs to the tenant aggregate, so the tenant row is locked first; under the lock the Owners are counted again, as
// every change to a membership counts them (K1). The rule is the tenant's (M1, Tenant.RemoveForUserDeletion): a user who became the
// tenant's last Owner since the check stays, and the deletion stops before it erases anything (A8). A user who is no longer a member
// changes nothing. Rejections come first (W7). Under the same lock the tenant's invitations of the user lose their address (A8,
// Tenant.EraseForUserDeletion); the user's address is read as a value, without a lock of the user (K6a). The identity provider's
// invitations behind the cancelled ones are withdrawn after the commit. The user's deletion is audited, so the erasure writes no entry.
public static class RemoveUserMembershipHandler
{
    public static async Task<(Result, OutgoingMessages)> HandleAsync(
        RemoveUserMembershipCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("A user leaves a tenant they are a member of.");
        var facts = await tenants.FindMemberFactsAsync(command.UserId, cancellationToken);
        var removed = tenant.RemoveForUserDeletion(facts);
        if (!removed.IsSuccess)
        {
            return (removed.Error, []);
        }

        if (removed.Value is not { } membership)
        {
            return (Result.Success(), []);
        }

        var profile = await users.FindProfileAsync(command.UserId, cancellationToken)
            ?? throw new InvalidOperationException("A member is always a user.");
        var cancelled = tenant.EraseForUserDeletion(await tenants.FindInvitationsOfUserAsync(command.UserId, profile.Email, cancellationToken));
        await tenants.RemoveMembershipAsync(tenant, membership, cancellationToken);

        var now = time.GetUtcNow();
        var role = facts.Roles.Of(membership.RoleId);
        return (
            Result.Success(),
            [
                new UserMembershipRemovedEvent(Guid.CreateVersion7(now), now, tenant.Id, command.UserId, role.Name, role.Id, role.BuiltIn is not null),
                .. WithdrawSignUpCommand.For(cancelled),
            ]);
    }
}
