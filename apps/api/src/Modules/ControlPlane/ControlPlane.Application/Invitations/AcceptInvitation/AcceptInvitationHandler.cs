using ControlPlane.Application.Tenants;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.Members;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using FluentValidation;
using FluentValidation.Results;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Invitations.AcceptInvitation;

// The rules are the tenant's (M1, Tenant.CheckInvitationFound, Tenant.Accept). The acceptance changes two aggregates in one transaction,
// because a business rule spans them: the user's creation or count follows the membership (M2, R13). Rejections come first (W7).
public static class AcceptInvitationHandler
{
    public static async Task<(Result<MyTenantView>, OutgoingMessages)> HandleAsync(
        AcceptInvitationCommand command,
        ITenantRepository tenants,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // K6a: a membership and an invitation belong to the tenant aggregate, so the tenant row is locked first, before the invitation.
        // A suspension then runs before or after the acceptance, never between its check and its change. A code that names no tenant
        // answers like an invitation that does not exist, and so does a cancelled invitation.
        if (await tenants.FindTenantForUpdateAsync(cancellationToken) is not { } tenant
            || await tenants.FindInvitationForUpdateAsync(InvitationSecret.Hash(command.Secret), cancellationToken) is not { } invitation)
        {
            return (Errors.InvitationNotFound, []);
        }

        var found = tenant.CheckInvitationFound(invitation);
        if (!found.IsSuccess)
        {
            return (found.Error, []);
        }

        var now = time.GetUtcNow();

        // A8: a system admin's deletion locks the user row and erases the user once they have no membership. The lock waits for a
        // deletion that runs, and a deleted user is refused, so a deleted user never becomes a member. The door refused them already,
        // unless the deletion committed after it let them in. The acceptance counts the membership on the user row (R13), so it takes the
        // row's update lock at once: two acceptances of one user in two tenants then run one after the other, where two share locks
        // that both wait to update would deadlock. Two acceptances of a person who is not a user yet run one after the other too
        // (K6f), so the second finds the first one's user and goes on as for a known user.
        var existing = await users.FindUserForNoKeyUpdateAsync(command.ActorId, cancellationToken);
        var facts = existing is null
            ? AcceptanceFacts.None
            : new AcceptanceFacts(
                existing.DeletedAt is not null,
                await users.IsSystemAdminAsync(existing.Id, cancellationToken),
                await tenants.FindMembershipAsync(existing.Id, cancellationToken) is not null);

        // The identity provider's addresses enter here, in their one form (EmailAddress).
        var verifiedEmails = EmailAddress.AllOf(command.VerifiedEmails);
        var allowed = tenant.CheckAcceptance(invitation, facts, verifiedEmails, now);
        if (!allowed.IsSuccess)
        {
            return (allowed.Error, []);
        }

        // A person who is not a user yet gives their names, as a validator would refuse them: a 400 that names each missing field. It
        // comes after the tenant's checks, so a stranger with the link learns nothing more, and before any change (W7).
        if (existing is null && (command.FirstName is null || command.LastName is null))
        {
            throw new ValidationException(MissingNames(command));
        }

        var user = existing ?? User.Join(Guid.CreateVersion7(now), command.ActorId, EmailAddress.Create(invitation.Email ?? throw new InvalidOperationException("A pending invitation always has an address.")), command.FirstName!, command.LastName!, invitation.Language, now);
        var accepted = tenant.Accept(invitation, facts, verifiedEmails, user.Id, now);
        if (!accepted.IsSuccess)
        {
            return (accepted.Error, []);
        }

        if (existing is null)
        {
            users.Add(user);
        }

        var membership = accepted.Value;
        await tenants.AddMembershipAsync(tenant, membership.UserId, membership.RoleId, membership.JoinedAt, cancellationToken);
        var roles = await tenants.FindRolesAsync(cancellationToken);
        return (
            MyTenantView.Of(tenant, roles.Of(membership.RoleId)),
            [new MemberJoinedEvent(Guid.CreateVersion7(now), now, tenant.Id, user.Id)]);
    }

    private static IEnumerable<ValidationFailure> MissingNames(AcceptInvitationCommand command) =>
        new (string Field, string? Value)[] { (nameof(command.FirstName), command.FirstName), (nameof(command.LastName), command.LastName) }
            .Where(field => field.Value is null)
            .Select(field => new ValidationFailure(field.Field, $"{field.Field} is required for a person who is not a user yet.")
            {
                ErrorCode = nameof(FieldErrorCode.Required),
            });
}
