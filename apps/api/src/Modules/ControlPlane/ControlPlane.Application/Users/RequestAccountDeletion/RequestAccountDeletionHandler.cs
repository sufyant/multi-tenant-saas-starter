using ControlPlane.Contracts.Users;
using ControlPlane.Domain.VerificationCodes;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Users.RequestAccountDeletion;

// The command locks the user row (K6l) and sends a code to the user's own address, in their language, after the commit. It records
// user.deletion_requested; the deletion itself records user.deleted. Rejections come first (W7).
public static class RequestAccountDeletionHandler
{
    public static async Task<(Result<VerificationCodeView>, OutgoingMessages)> HandleAsync(
        RequestAccountDeletionCommand command,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.ActorId, cancellationToken) is not { Email: { } email } user)
        {
            return (Errors.UserNotFound, []);
        }

        var now = time.GetUtcNow();
        var issued = await VerificationCodeChecks.IssueAsync(users, user.Id, VerificationCodePurpose.AccountDeletion, null, now, cancellationToken);
        if (!issued.IsSuccess)
        {
            return (issued.Error, []);
        }

        await users.SaveChangesAsync(cancellationToken);
        var (code, digits) = issued.Value;
        return (
            new VerificationCodeView(code.ExpiresAt),
            [
                new UserDeletionRequestedEvent(Guid.CreateVersion7(now), now, user.Id),
                new VerificationCodeReadyEvent(code.Id, VerificationEmailKind.AccountDeletion, email, user.Language, digits, code.ExpiresAt),
            ]);
    }
}
