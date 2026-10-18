using ControlPlane.Application.Invitations;
using ControlPlane.Application.Invitations.SignUp;
using ControlPlane.Application.Ports;
using ControlPlane.Contracts;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;
using FluentValidation;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// A system admin invites an Owner into the tenant the platform route names, which has no Owner because its first Owner never accepted,
/// in a language for the invitation's email: without one, the system admin's own (K8). The host's idempotency layer protects the HTTP
/// request (I1); the command carries no request key, and the server chooses the invitation's id.
/// </summary>
public sealed record InviteOwnerCommand(string AdminId, string Email, Language? Language);

public sealed class InviteOwnerValidator : AbstractValidator<InviteOwnerCommand>
{
    public InviteOwnerValidator()
    {
        // The same check of an address as a tenant's first owner has. The invitation trims it.
        RuleFor(command => command.Email)
            .NotEmpty().WithErrorCode(nameof(FieldErrorCode.Required))
            .EmailAddress().WithErrorCode(nameof(FieldErrorCode.EmailInvalid))
            .MaximumLength(Invitation.EmailMaxLength).WithErrorCode(nameof(FieldErrorCode.TooLong));
    }
}

// K6: an invitation belongs to the tenant aggregate, so the tenant row is locked first, and the Owners are counted under the lock. An
// acceptance locks the same row first, so it runs before, and the tenant has its Owner, or after, and finds its invitation cancelled. At
// most one platform Owner invitation is pending: in a tenant without an Owner nobody else invites, so every pending invitation is the
// platform's, and the new one replaces it, also to another address (the maintainer's decision in step 20k). The replaced one is saved as
// cancelled first, so the partial unique index never sees two pending invitations. Every rejection returns before the handler changes
// anything, because Wolverine commits a failed Result too (W7). The invitation then takes the path of a member's: the identity provider's
// sign-up, then the email (OpenMemberSignUpCommand). The owner's messages of the tenant creation answer its saga, which has ended (S13).
public static class InviteOwnerHandler
{
    public static async Task<(Result<InvitationSummary>, OwnerInvitedEvent?, OpenMemberSignUpCommand?, OutgoingMessages)> HandleAsync(
        InviteOwnerCommand command,
        ITenantCatalog catalog,
        InvitationOptions invitations,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await catalog.FindTenantForUpdateAsync(cancellationToken) is not { } tenant)
        {
            return (Errors.TenantNotFound, null, null, []);
        }

        var allowed = tenant.CheckOwnerInvitation(await catalog.CountOwnersAsync(cancellationToken));
        if (!allowed.IsSuccess)
        {
            return (allowed.Error, null, null, []);
        }

        var admin = await catalog.FindUserAsync(command.AdminId, cancellationToken)
            ?? throw new InvalidOperationException("A system admin is always a catalog user.");

        var withdrawals = new OutgoingMessages();
        var replaced = await catalog.FindPendingInvitationsAsync(cancellationToken);
        foreach (var pending in replaced)
        {
            pending.Cancel();
            if (pending.SignUpInvitationId is { } signUp)
            {
                withdrawals.Add(new WithdrawSignUpCommand(signUp));
            }
        }

        await catalog.SaveChangesAsync(cancellationToken);

        var now = time.GetUtcNow();
        var secret = InvitationToken.Generate();
        var invitation = Invitation.Create(
            Guid.CreateVersion7(now),
            tenant.Id,
            command.Email,
            BuiltInRoles.Owner,
            admin.Id,
            now,
            invitations.Lifetime,
            secret,
            command.Language ?? admin.Language);
        catalog.Add(invitation);
        tenant.Summarize(await catalog.ReadRowsAsync(cancellationToken));
        await catalog.SaveChangesAsync(cancellationToken);

        return (
            InvitationSummary.Of(invitation, admin, now),
            new OwnerInvitedEvent(Guid.CreateVersion7(now), now, tenant.Id, tenant.Name, invitation.Id, admin.Id, replaced.SingleOrDefault()?.Id),
            new OpenMemberSignUpCommand(
                tenant.Id,
                invitation.Id,
                invitation.Email,
                tenant.Name,
                invitations.AcceptLink(new InvitationCode(tenant.Id, secret)),
                invitation.ExpiresAt),
            withdrawals);
    }
}
