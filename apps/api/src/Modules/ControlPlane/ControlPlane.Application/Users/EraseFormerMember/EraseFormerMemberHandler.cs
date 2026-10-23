using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using Wolverine;

namespace ControlPlane.Application.Users.EraseFormerMember;

// K6l: an invitation belongs to the tenant aggregate, so the tenant row is locked first. Under the lock the tenant's invitations of the
// user lose their address, by the rule of RemoveUserMembershipCommand without its membership part (A8, Tenant.EraseFormerMember). A
// tenant with no such invitation, a purged one included, changes nothing and saves no summary. A second arrival finds the addresses
// erased and cancels nothing. The identity provider's invitations behind the cancelled ones are withdrawn after the commit. The user's
// deletion is audited, so the erasure writes no entry.
public static class EraseFormerMemberHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        EraseFormerMemberCommand command,
        ITenantRepository tenants,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("A user's deletion erases in a tenant where the user accepted an invitation.");
        var invitations = await tenants.FindInvitationsOfUserAsync(command.UserId, command.Email, cancellationToken);
        if (invitations.Count == 0)
        {
            return [];
        }

        var cancelled = tenant.EraseFormerMember(await tenants.FindMembershipAsync(command.UserId, cancellationToken), invitations);
        await tenants.SaveAsync(tenant, cancellationToken);
        return [.. WithdrawSignUpCommand.For(cancelled)];
    }
}
