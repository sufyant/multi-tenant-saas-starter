using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using FluentValidation;
using SharedKernel;

namespace ControlPlane.Application.Invitations;

/// <summary>
/// An Owner or an Admin invites someone to the active tenant by email, with a built-in role (<c>Owner</c>, <c>Admin</c>, <c>Member</c>).
/// The host's idempotency layer protects the HTTP request (I1); the command carries no request key, and the server chooses the
/// invitation's id.
/// </summary>
public sealed record InviteMember(string InviterId, string Email, string Role);

public sealed class InviteMemberValidator : AbstractValidator<InviteMember>
{
    public InviteMemberValidator()
    {
        // The same check of an address as a tenant's first owner has. The invitation trims it.
        RuleFor(command => command.Email)
            .NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required))
            .EmailAddress().WithErrorCode(nameof(FieldErrorCode.EmailInvalid))
            .MaximumLength(Invitation.EmailMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}

// K6: an invitation belongs to the tenant aggregate, so the tenant row is locked first; two invitations at once then run one after the
// other, and neither passes the daily limit nor leaves a second pending invitation. The checks run in the catalog's order: an inviter
// who is no longer a member, a tenant that is no longer active, the role rule, a member's address, the daily limit. Every rejection returns before the handler changes
// anything, because Wolverine commits a failed Result too (W7). The secret is born here; only its hash is stored, and the link that
// carries it goes on in the event.
public static class InviteMemberHandler
{
    private static readonly TimeSpan LimitWindow = TimeSpan.FromHours(24);

    public static async Task<(Result<InvitationSummary>, MemberInvited?)> HandleAsync(
        InviteMember command,
        ITenantCatalog catalog,
        InvitationSettings invitations,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var tenant = await catalog.FindTenantForUpdateAsync(cancellationToken)
            ?? throw new InvalidOperationException("The tenant door lets a member into an existing tenant only.");
        var inviter = await catalog.FindUserAsync(command.InviterId, cancellationToken)
            ?? throw new InvalidOperationException("A member is always a catalog user.");

        // The door let the inviter in, but a change that held the lock before may have removed them: to a non-member the tenant does not
        // exist (T3).
        if (await catalog.FindMembershipAsync(inviter.Id, cancellationToken) is not { } membership)
        {
            return (Errors.TenantNotFound, null);
        }

        // The door let the inviter into an active tenant, but a system admin may have suspended or closed it while this request waited for
        // the lock: under the lock, a tenant that is no longer active is refused (K6).
        var access = Tenant.CheckMemberAccess(tenant.Status);
        if (!access.IsSuccess)
        {
            return (access.Error, null);
        }

        var role = BuiltInRoles.All.Single(builtIn => builtIn.Name == command.Role);

        var allowed = Invitation.CheckInviter(BuiltInRoles.All.Single(builtIn => builtIn.Id == membership.RoleId), role);
        if (!allowed.IsSuccess)
        {
            return (allowed.Error, null);
        }

        var email = command.Email.Trim();
        if (await catalog.IsMemberEmailAsync(email, cancellationToken))
        {
            return (Error.Conflict(ControlPlaneErrorCodes.MembershipExists, "Someone with this email address is already a member of this tenant."), null);
        }

        var now = time.GetUtcNow();
        if (await catalog.CountInvitationsSinceAsync(now - LimitWindow, cancellationToken) >= invitations.DailyLimitPerTenant)
        {
            return (Error.TooManyRequests(ControlPlaneErrorCodes.InvitationDailyLimitReached, "The tenant has sent as many invitations as it may in 24 hours."), null);
        }

        // K2: the new invitation replaces a pending one to the same address. The replaced one is saved as cancelled first, so the
        // partial unique index never sees two pending invitations.
        var replaced = await catalog.FindPendingInvitationAsync(email, cancellationToken);
        if (replaced is not null)
        {
            replaced.Cancel();
            await catalog.SaveChangesAsync(cancellationToken);
        }

        var secret = InvitationToken.Generate();
        var invitation = Invitation.Create(Guid.CreateVersion7(now), tenant.Id, email, role, inviter.Id, now, invitations.Lifetime, secret);
        catalog.Add(invitation);
        await catalog.SaveChangesAsync(cancellationToken);

        return (
            InvitationSummary.Of(invitation),
            new MemberInvited(
                Guid.CreateVersion7(now),
                now,
                tenant.Id,
                invitation.Id,
                inviter.Id,
                role.Name,
                replaced?.Id,
                invitation.Email,
                tenant.Name,
                invitations.AcceptLink(new InvitationCode(tenant.Id, secret))));
    }
}
