using ControlPlane.Domain.VerificationCodes;
using SharedKernel;

namespace ControlPlane.Application.Users.ConfirmAccountDeletion;

// The command locks the user row (K6l) and uses the user's latest code of a deletion. A wrong, expired or spent code answers 400, and a
// wrong try is kept (VerificationCodes). It writes no audit entry of its own: it is a step of the user's deletion, which records
// user.deleted (CommandAuditTests).
public static class ConfirmAccountDeletionHandler
{
    public static async Task<Result> HandleAsync(
        ConfirmAccountDeletionCommand command,
        IUserRepository users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await users.FindUserForUpdateAsync(command.ActorId, cancellationToken) is not { } user)
        {
            return Errors.UserNotFound;
        }

        var used = await VerificationCodes.UseAsync(users, user.Id, VerificationCodePurpose.AccountDeletion, command.Code, time.GetUtcNow(), cancellationToken);
        return used.IsSuccess ? Result.Success() : used.Error;
    }
}
