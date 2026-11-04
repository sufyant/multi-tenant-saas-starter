using System.Security.Cryptography;
using System.Text;
using SharedKernel;

namespace ControlPlane.Domain.VerificationCodes;

/// <summary>
/// A code of six digits that ControlPlane sends to an address, so the person proves that they read it: for a change of their address, to
/// the new one, and for the deletion of their account, to their own (section 6, "Verification codes"). Only its hash is stored. Only the
/// user's latest code of a purpose counts, for its lifetime and a few wrong tries. The row belongs to its user, not to a tenant (R6, R11).
/// </summary>
internal sealed class VerificationCode(Guid id, Guid userId, VerificationCodePurpose purpose)
{
    public const int Digits = 6;

    /// <summary>The wrong tries a code allows; the next try finds it spent, and the person asks for a new code.</summary>
    public const int MaxAttempts = 5;

    /// <summary>The codes of one purpose that a user may ask for in <see cref="SendWindow"/>, so nobody floods an address with codes.</summary>
    public const int MaxSends = 5;

    /// <summary>How long a code works.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>The window that <see cref="MaxSends"/> counts in. A code older than it is no use, so a new code deletes it.</summary>
    public static readonly TimeSpan SendWindow = TimeSpan.FromHours(1);

    public Guid Id { get; private init; } = id;

    public Guid UserId { get; private init; } = userId;

    public VerificationCodePurpose Purpose { get; private init; } = purpose;

    /// <summary>The SHA-256 of the code with the row's id; never the code itself.</summary>
    public byte[] CodeHash { get; private init; } = [];

    /// <summary>The address a change of address goes to, in its one form; none for a deletion.</summary>
    public string? NewEmail { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    /// <summary>The wrong tries so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>When the right code was given. A used code works no more.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>
    /// A new code for the user and the purpose, at this moment, and the code itself, which only its email carries. The digits come from a
    /// cryptographic random number generator, every value of the six digits equally likely.
    /// </summary>
    public static (VerificationCode Code, string Digits) Issue(Guid id, Guid userId, VerificationCodePurpose purpose, string? newEmail, DateTimeOffset now)
    {
        var digits = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        return (
            new VerificationCode(id, userId, purpose)
            {
                CodeHash = HashOf(id, digits),
                NewEmail = newEmail,
                CreatedAt = now,
                ExpiresAt = now + Lifetime,
            },
            digits);
    }

    /// <summary>
    /// The person gives a code at this moment. A used, expired or spent code is refused before the digits are compared. A wrong code counts
    /// a try, which the handler keeps although it refuses (W7); the right one marks the code used, so it works once.
    /// </summary>
    public Result Use(string digits, DateTimeOffset now)
    {
        if (UsedAt is not null)
        {
            return Error.Validation(ControlPlaneErrorCodes.VerificationCodeInvalid, "The code is not right.");
        }

        if (now >= ExpiresAt)
        {
            return Error.Validation(ControlPlaneErrorCodes.VerificationCodeExpired, "The code has expired; ask for a new one.");
        }

        if (Attempts >= MaxAttempts)
        {
            return Error.Validation(ControlPlaneErrorCodes.VerificationCodeTooManyAttempts, "The code was tried too often; ask for a new one.");
        }

        if (!CryptographicOperations.FixedTimeEquals(CodeHash, HashOf(Id, digits.Trim())))
        {
            Attempts++;
            return Error.Validation(ControlPlaneErrorCodes.VerificationCodeInvalid, "The code is not right.");
        }

        UsedAt = now;
        return Result.Success();
    }

    private static byte[] HashOf(Guid id, string digits) => SHA256.HashData(Encoding.UTF8.GetBytes($"{id:N}:{digits}"));
}
