using ControlPlane.Application.Ports;
using ControlPlane.Application.Tenants;
using ControlPlane.Contracts;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using SharedKernel;

namespace ControlPlane.Application.Invitations;

/// <summary>
/// Accepts an invitation in its tenant, the one its invitation code names: the invitation is found by the hash of the code's
/// secret, and the catalog user, when new, and the membership are created in the same transaction as the invitation is used up.
/// The user's verified email addresses and profile are read from the identity provider before that transaction begins, so no call
/// leaves the process while the invitation is locked. A new catalog user starts with a copy of that profile.
/// </summary>
public sealed record AcceptInvitation(string Secret, string UserId, IReadOnlyList<string> VerifiedEmails, UserProfile Profile);

// Every rejection returns before the handler changes anything, because Wolverine commits a failed Result too (W7).
public static class AcceptInvitationHandler
{
    public static async Task<(Result<TenantSummary>, MemberJoined?)> HandleAsync(
        AcceptInvitation command,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // K6: a membership and an invitation belong to the tenant aggregate, so the tenant row is locked first, before the invitation.
        // A suspension then runs before or after the acceptance, never between its check and its change. A code that names no tenant
        // answers like an invitation that does not exist, and so does a cancelled invitation.
        if (await catalog.FindTenantForUpdateAsync(cancellationToken) is not { } tenant
            || await catalog.FindInvitationForUpdateAsync(InvitationToken.Hash(command.Secret), cancellationToken) is not { } invitation
            || invitation.Status == InvitationStatus.Cancelled)
        {
            return (Errors.InvitationNotFound, null);
        }

        var now = time.GetUtcNow();
        var existing = await catalog.FindUserAsync(command.UserId, cancellationToken);

        // T5: provider staff enter through the system door, never as tenant members.
        if (existing is not null && await catalog.IsSystemAdminAsync(existing.Id, cancellationToken))
        {
            return (Error.Unprocessable("invitation.staff_cannot_join", "Provider staff cannot join a tenant."), null);
        }

        if (existing is not null && await catalog.FindMembershipAsync(existing.Id, cancellationToken) is not null)
        {
            return (Error.Conflict("membership.exists", "You are already a member of this tenant."), null);
        }

        var allowed = invitation.CheckAcceptance(command.VerifiedEmails, now);
        if (!allowed.IsSuccess)
        {
            return (allowed.Error, null);
        }

        // Only the invited person with a live invitation learns that the tenant is suspended (as T3). The invitation stays pending,
        // so it can be accepted once the tenant is reactivated.
        if (tenant.Status == TenantStatus.Suspended)
        {
            return (Error.Conflict("tenant.suspended", "The tenant is suspended."), null);
        }

        var user = existing ?? new User(Guid.CreateVersion7(now), command.UserId);
        var accepted = invitation.Accept(command.VerifiedEmails, user.Id, now);
        if (!accepted.IsSuccess)
        {
            return (accepted.Error, null);
        }

        if (existing is null)
        {
            user.CopyProfile(command.Profile.Email, command.Profile.FirstName, command.Profile.LastName, command.Profile.UpdatedAt);
            catalog.Add(user);
        }

        catalog.AddMember(user.Id, invitation.RoleId);
        await catalog.SaveChangesAsync(cancellationToken);
        return (
            new TenantSummary(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString()),
            new MemberJoined(Guid.CreateVersion7(now), now, tenant.Id, user.Id));
    }
}
