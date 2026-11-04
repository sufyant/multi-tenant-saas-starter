using ControlPlane.Application.Users.GetMe;
using ControlPlane.Application.Users.ReplaceIdentityEmail;
using ControlPlane.Contracts.Users;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.VerificationCodes;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Users.ChangeEmail;

// The command locks the user row (K6l). An address that another account took meanwhile answers 409 before the code is used, so the code
// stays; the lock of the address orders two users who take it at once (IUserRepository.IsEmailTakenAsync). Then it uses the user's latest
// code of a change of address: a wrong, expired or spent code answers 400, and a wrong try is kept (VerificationCodeChecks). A deleted
// user is refused at the door (A9); one deleted while the request waited for the row is refused after the code was used. In the same transaction it records user.email_changed, sends the identity provider's change, which the
// outbox retries, so our record and the provider do not stay apart, and tells the old address (A7).
public static class ChangeEmailHandler
{
    public static async Task<(Result<MeView>, OutgoingMessages)> HandleAsync(
        ChangeEmailCommand command,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.ActorId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, []);
        }

        // The refusals that keep the code come before the code is used: an address another user took meanwhile (W7).
        if (await users.FindLatestVerificationCodeForUpdateAsync(user.Id, VerificationCodePurpose.EmailChange, cancellationToken) is { NewEmail: { } pending }
            && await users.IsEmailTakenAsync(EmailAddress.Create(pending), user.Id, cancellationToken))
        {
            return (Errors.UserEmailTaken, []);
        }

        var now = time.GetUtcNow();
        var used = await VerificationCodeChecks.UseAsync(users, user.Id, VerificationCodePurpose.EmailChange, command.Code, now, cancellationToken);
        if (!used.IsSuccess)
        {
            return (used.Error, []);
        }

        var oldEmail = user.Email;
        var email = EmailAddress.Create(used.Value.NewEmail!);
        var changed = user.ChangeEmail(email);
        if (!changed.IsSuccess)
        {
            return (changed.Error, []);
        }

        var me = new MeView(user.Id, user.Email, user.FirstName, user.LastName, await users.IsSystemAdminAsync(user.Id, cancellationToken), user.Language, user.TimeZone);
        if (changed.Value == StateChange.Unchanged)
        {
            return (me, []);
        }

        await users.SaveChangesAsync(cancellationToken);
        OutgoingMessages messages =
        [
            new UserEmailChangedEvent(Guid.CreateVersion7(now), now, user.Id),
            new ReplaceIdentityEmailCommand(user.ExternalId, email.Value),
        ];
        if (oldEmail is not null)
        {
            messages.Add(new EmailChangeNoticeReadyEvent(user.Id, used.Value.Id, oldEmail, email.Value, user.Language));
        }

        return (me, messages);
    }
}
