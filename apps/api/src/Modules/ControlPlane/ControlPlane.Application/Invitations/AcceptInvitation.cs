using ControlPlane.Application.Ports;
using ControlPlane.Application.Tenants;
using ControlPlane.Contracts;
using ControlPlane.Domain;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using FluentValidation;
using FluentValidation.Results;
using SharedKernel;

namespace ControlPlane.Application.Invitations;

/// <summary>
/// Accepts an invitation in its tenant, the one its invitation code names: the invitation is found by the hash of the code's
/// secret, and the catalog user, when new, and the membership are created in the same transaction as the invitation is used up.
/// The user's verified email addresses are read from the identity provider before that transaction begins, so no call leaves the
/// process while the invitation is locked. A new catalog user gets the invited address, the names the person typed (A7) and the
/// invitation's language; a user the catalog knows needs no names and keeps their language.
/// </summary>
public sealed record AcceptInvitation(string Secret, string UserId, IReadOnlyList<string> VerifiedEmails, string? FirstName, string? LastName);

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitation>
{
    public AcceptInvitationValidator()
    {
        // A name is checked when it is given; whether it must be given depends on the catalog, which the handler reads. The user keeps a
        // name without the spaces around it, so its length is counted without them.
        RuleFor(command => command.FirstName!)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(User.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong))
            .When(command => command.FirstName is not null);
        RuleFor(command => command.LastName!)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode(nameof(FieldErrorCode.Required))
            .MaximumTrimmedLength(User.NameMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong))
            .When(command => command.LastName is not null);
    }
}

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

        // A8: a system admin's deletion locks the user row and erases the user once they have no membership. The lock waits for a
        // deletion that runs, and a deleted user is refused, so a deleted user never becomes a member. The door refused them already,
        // unless the deletion committed after it let them in. The acceptance counts the membership on the user row (R13), so it takes the
        // row's update lock at once: two acceptances of one user in two tenants then run one after the other, where two share locks
        // that both wait to update would deadlock.
        var existing = await catalog.FindUserForUpdateAsync(command.UserId, cancellationToken);
        if (existing?.DeletedAt is not null)
        {
            return (Error.Forbidden(ControlPlaneErrorCodes.UserDeleted, "Your account is deleted."), null);
        }

        // T5: system admins enter through the system door, never as tenant members.
        if (existing is not null && await catalog.IsSystemAdminAsync(existing.Id, cancellationToken))
        {
            return (Error.Unprocessable(ControlPlaneErrorCodes.InvitationSystemAdminCannotJoin, "A system admin cannot join a tenant."), null);
        }

        if (existing is not null && await catalog.FindMembershipAsync(existing.Id, cancellationToken) is not null)
        {
            return (Error.Conflict(ControlPlaneErrorCodes.MemberExists, "You are already a member of this tenant."), null);
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
            return (Error.Conflict(ControlPlaneErrorCodes.TenantSuspended, "The tenant is suspended."), null);
        }

        // A person the catalog does not know gives their names, as a validator would refuse them: a 400 that names each missing field. It
        // comes after the invitation's own checks, so a stranger with the link learns nothing more, and before any change (W7).
        if (existing is null && (command.FirstName is null || command.LastName is null))
        {
            throw new ValidationException(MissingNames(command));
        }

        var user = existing ?? User.Join(Guid.CreateVersion7(now), command.UserId, invitation.Email, command.FirstName!, command.LastName!, invitation.Language, now);
        var accepted = invitation.Accept(command.VerifiedEmails, user.Id, now);
        if (!accepted.IsSuccess)
        {
            return (accepted.Error, null);
        }

        if (existing is null)
        {
            catalog.Add(user);
        }

        await catalog.AddMembershipAsync(tenant, user.Id, invitation.RoleId, now, cancellationToken);
        return (
            TenantSummary.Of(tenant, BuiltInRoles.All.Single(role => role.Id == invitation.RoleId)),
            new MemberJoined(Guid.CreateVersion7(now), now, tenant.Id, user.Id));
    }

    private static IEnumerable<ValidationFailure> MissingNames(AcceptInvitation command) =>
        new (string Field, string? Value)[] { (nameof(command.FirstName), command.FirstName), (nameof(command.LastName), command.LastName) }
            .Where(field => field.Value is null)
            .Select(field => new ValidationFailure(field.Field, $"{field.Field} is required for a person the catalog does not know.")
            {
                ErrorCode = nameof(FieldErrorCode.Required),
            });
}
