using ControlPlane.Contracts.Users;
using ControlPlane.Domain;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.VerificationCodes;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Users.RequestEmailChange;

// The command locks the user row (K6l). It refuses the address the user has, and one that another account uses, before it sends a code,
// so the person learns it at once; the change checks again. Rejections come first (W7). The code goes to the new address in the user's
// language, after the commit. It writes no audit entry: it changes no data of the user, and the change itself records user.email_changed
// (CommandAuditTests).
public static class RequestEmailChangeHandler
{
    public static async Task<(Result<VerificationCodeView>, OutgoingMessages)> HandleAsync(
        RequestEmailChangeCommand command,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.ActorId, cancellationToken) is not { } user)
        {
            return (Errors.UserNotFound, []);
        }

        var email = EmailAddress.Create(command.Email);
        if (user.Email == email.Value)
        {
            return (Error.Conflict(ControlPlaneErrorCodes.UserEmailUnchanged, "This is your address already."), []);
        }

        if (await users.IsEmailTakenAsync(email, user.Id, cancellationToken))
        {
            return (Errors.UserEmailTaken, []);
        }

        var now = time.GetUtcNow();
        var issued = await VerificationCodeChecks.IssueAsync(users, user.Id, VerificationCodePurpose.EmailChange, email.Value, now, cancellationToken);
        if (!issued.IsSuccess)
        {
            return (issued.Error, []);
        }

        await users.SaveChangesAsync(cancellationToken);
        var (code, digits) = issued.Value;
        return (
            new VerificationCodeView(code.ExpiresAt),
            [new VerificationCodeReadyEvent(code.Id, VerificationEmailKind.EmailChange, email.Value, user.Language, digits, code.ExpiresAt)]);
    }
}
