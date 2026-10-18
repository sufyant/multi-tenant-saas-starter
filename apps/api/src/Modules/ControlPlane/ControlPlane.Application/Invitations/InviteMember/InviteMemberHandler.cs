using ControlPlane.Application.Invitations.OpenMemberSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Members;
using ControlPlane.Domain;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Invitations.InviteMember;

// K6: an invitation belongs to the tenant aggregate, so the tenant row is locked first; two invitations at once then run one after the
// other, and neither passes the daily limit nor leaves a second pending invitation. The checks run in the catalog's order: an inviter
// who is no longer a member, a tenant that is no longer active, the role rule, a member's address, the daily limit. Every rejection returns before the handler changes
// anything, because Wolverine commits a failed Result too (W7). The secret is born here; only its hash is stored, and the link that
// carries it goes on to the identity provider's sign-up, after the commit. A replaced invitation's sign-up is withdrawn.
public static class InviteMemberHandler
{
    private static readonly TimeSpan LimitWindow = TimeSpan.FromHours(24);

    public static async Task<(Result<InvitationView>, MemberInvitedEvent?, OpenMemberSignUpCommand?, WithdrawSignUpCommand?)> HandleAsync(
        InviteMemberCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        InvitationOptions invitations,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant door lets a member into an existing tenant only.");
        var inviter = await users.FindUserAsync(command.ActorId, cancellationToken)
            ?? throw new InvalidOperationException("A member is always a catalog user.");

        // The door let the inviter in, but a change that held the lock before may have removed them: to a non-member the tenant does not
        // exist (T3).
        if (await tenants.FindMembershipAsync(inviter.Id, cancellationToken) is not { } membership)
        {
            return (Errors.TenantNotFound, null, null, null);
        }

        // The door let the inviter into an active tenant, but a system admin may have suspended or closed it while this request waited for
        // the lock: under the lock, a tenant that is no longer active is refused (K6).
        var access = Tenant.CheckMemberAccess(tenant.Status);
        if (!access.IsSuccess)
        {
            return (access.Error, null, null, null);
        }

        var role = BuiltInRoles.All.Single(builtIn => builtIn.BuiltIn == Enum.Parse<BuiltInRole>(command.Role));

        var allowed = Invitation.CheckInviter(BuiltInRoles.All.Single(builtIn => builtIn.Id == membership.RoleId), role);
        if (!allowed.IsSuccess)
        {
            return (allowed.Error, null, null, null);
        }

        var email = command.Email.Trim();
        if (await tenants.IsMemberEmailAsync(email, cancellationToken))
        {
            return (Error.Conflict(ControlPlaneErrorCodes.MemberExists, "Someone with this email address is already a member of this tenant."), null, null, null);
        }

        var now = time.GetUtcNow();
        if (await tenants.CountInvitationsSinceAsync(now - LimitWindow, cancellationToken) >= invitations.DailyLimitPerTenant)
        {
            return (Error.TooManyRequests(ControlPlaneErrorCodes.InvitationDailyLimitReached, "The tenant has sent as many invitations as it may in 24 hours."), null, null, null);
        }

        // K2: the new invitation replaces a pending one to the same address. The replaced one is saved as cancelled first, so the
        // partial unique index never sees two pending invitations.
        var replaced = await tenants.FindPendingInvitationAsync(email, cancellationToken);
        if (replaced is not null)
        {
            replaced.Cancel();
            await tenants.SaveChangesAsync(cancellationToken);
        }

        var secret = InvitationSecret.Generate();
        var language = command.Language ?? inviter.Language;
        var invitation = Invitation.Create(Guid.CreateVersion7(now), tenant.Id, email, role, inviter.Id, now, invitations.Lifetime, secret, language);
        tenants.Add(invitation);
        tenant.Summarize(await tenants.ReadRowsAsync(cancellationToken));
        await tenants.SaveChangesAsync(cancellationToken);

        return (
            InvitationView.Of(invitation, inviter, now),
            new MemberInvitedEvent(
                Guid.CreateVersion7(now),
                now,
                tenant.Id,
                invitation.Id,
                inviter.Id,
                role.Name,
                replaced?.Id),
            new OpenMemberSignUpCommand(
                tenant.Id,
                invitation.Id,
                invitation.Email,
                tenant.Name,
                invitations.AcceptLink(new InvitationCode(tenant.Id, secret)),
                invitation.ExpiresAt),
            replaced?.SignUpInvitationId is { } withdrawn ? new WithdrawSignUpCommand(withdrawn) : null);
    }
}
