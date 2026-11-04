using ControlPlane.Domain;
using ControlPlane.Domain.VerificationCodes;
using SharedKernel;

namespace ControlPlane.Application.Users;

// The one way a user gets a verification code and gives it back, for a change of address and for the deletion of their account (section
// 6, "Verification codes"). Each caller has locked the user row, so two requests of one user run one at a time.
internal static class VerificationCodes
{
    // A new code, unless the user asked for MaxSends codes of the purpose in the window. Codes older than the window are of no use, so
    // they are deleted, and the table keeps a few rows of each user at most.
    public static async Task<Result<(VerificationCode Code, string Digits)>> IssueAsync(
        IUserRepository users, Guid userId, VerificationCodePurpose purpose, string? newEmail, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var windowStart = now - VerificationCode.SendWindow;
        if (await users.CountVerificationCodesSinceAsync(userId, purpose, windowStart, cancellationToken) >= VerificationCode.MaxSends)
        {
            return Error.TooManyRequests(ControlPlaneErrorCodes.VerificationCodeTooManySends, "Too many codes were sent; try again later.");
        }

        await users.DeleteVerificationCodesBeforeAsync(userId, windowStart, cancellationToken);
        var issued = VerificationCode.Issue(Guid.CreateVersion7(now), userId, purpose, newEmail, now);
        users.AddVerificationCode(issued.Code);
        return issued;
    }

    // The user's latest code of the purpose, used with the digits. Only the latest counts: a new code replaces the earlier ones. A wrong
    // try is saved here, because the handler then refuses, and Wolverine commits what was saved (W7).
    public static async Task<Result<VerificationCode>> UseAsync(
        IUserRepository users, Guid userId, VerificationCodePurpose purpose, string digits, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await users.FindLatestVerificationCodeForUpdateAsync(userId, purpose, cancellationToken) is not { } code)
        {
            return Error.Validation(ControlPlaneErrorCodes.VerificationCodeInvalid, "The code is not right.");
        }

        var used = code.Use(digits, now);
        await users.SaveChangesAsync(cancellationToken);
        return used.IsSuccess ? code : used.Error;
    }
}
