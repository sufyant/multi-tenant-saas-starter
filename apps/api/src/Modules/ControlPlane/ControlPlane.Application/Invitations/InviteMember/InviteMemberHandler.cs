using ControlPlane.Application.Invitations.OpenMemberSignUp;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Members;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Invitations.InviteMember;

// K6: an invitation belongs to the tenant aggregate, so the tenant row is locked first; two invitations at once then run one after the
// other, and neither passes the daily limit nor leaves a second pending invitation. The checks run in the order of api-catalog: an
// inviter who is no longer a member, a tenant that is no longer active, the role rule, a member's address, the daily limit. Rejections come
// first (W7). The secret is born here; only its hash is stored, and the link that carries it goes on to the identity provider's
// sign-up, after the commit. A replaced invitation's sign-up is withdrawn.
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
        var start = await TenantActor.LockAsync(tenants, users, command.ActorId, cancellationToken);
        if (!start.IsSuccess)
        {
            return (start.Error, null, null, null);
        }

        var (tenant, actor, actorMembership) = start.Value;

        var role = BuiltInRoles.All.Single(builtIn => builtIn.BuiltIn == Enum.Parse<BuiltInRole>(command.Role));

        var allowed = Invitation.CheckInviter(BuiltInRoles.All.Single(builtIn => builtIn.Id == actorMembership.RoleId), role);
        if (!allowed.IsSuccess)
        {
            return (allowed.Error, null, null, null);
        }

        var email = EmailAddress.Create(command.Email);
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
        var language = command.Language ?? actor.Language;
        var invitation = Invitation.Create(Guid.CreateVersion7(now), tenant.Id, email, role, actor.Id, now, invitations.Lifetime, secret, language);
        tenants.Add(invitation);
        await tenants.SaveAsync(tenant, cancellationToken);

        return (
            InvitationView.Of(invitation, UserNameView.Of(actor), now),
            new MemberInvitedEvent(
                Guid.CreateVersion7(now),
                now,
                tenant.Id,
                invitation.Id,
                actor.Id,
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
