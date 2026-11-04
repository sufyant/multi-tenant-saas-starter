using ControlPlane.Domain.VerificationCodes;
using SharedKernel;

namespace ControlPlane.UnitTests.VerificationCodes;

// Section 6, "Verification codes": six digits, stored as a hash, for 10 minutes, once, for 5 wrong tries.
public sealed class VerificationCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IssueCode_ForAUser_IsSixDigitsAndKeepsOnlyTheirHash()
    {
        var (code, digits) = Issue();

        digits.ShouldMatch("^[0-9]{6}$");
        code.CodeHash.Length.ShouldBe(32);
        Convert.ToHexString(code.CodeHash).ShouldNotContain(digits);
        code.ExpiresAt.ShouldBe(Now.AddMinutes(10));
    }

    [Fact]
    public void UseCode_TheRightDigits_MarksItUsed()
    {
        var (code, digits) = Issue();

        var used = code.Use(digits, Now.AddMinutes(9));

        used.IsSuccess.ShouldBeTrue();
        code.UsedAt.ShouldBe(Now.AddMinutes(9));
    }

    [Fact]
    public void UseCode_TheRightDigitsTwice_RefusesTheSecond()
    {
        var (code, digits) = Issue();
        code.Use(digits, Now);

        var again = code.Use(digits, Now);

        again.Error.Code.ShouldBe(ControlPlane.Domain.ControlPlaneErrorCodes.VerificationCodeInvalid);
    }

    [Fact]
    public void UseCode_AtItsExpiry_IsExpired()
    {
        var (code, digits) = Issue();

        var used = code.Use(digits, Now.AddMinutes(10));

        used.Error.Code.ShouldBe(ControlPlane.Domain.ControlPlaneErrorCodes.VerificationCodeExpired);
        code.UsedAt.ShouldBeNull();
    }

    // Each wrong try counts; after 5 the right digits are refused too.
    [Fact]
    public void UseCode_AfterFiveWrongTries_RefusesTheRightDigits()
    {
        var (code, digits) = Issue();
        var wrong = Enumerable.Range(0, 5).Select(_ => code.Use("wrong", Now).Error.Code).ToList();

        var right = code.Use(digits, Now);

        wrong.ShouldAllBe(error => error == ControlPlane.Domain.ControlPlaneErrorCodes.VerificationCodeInvalid);
        (code.Attempts, right.Error.Code).ShouldBe((5, ControlPlane.Domain.ControlPlaneErrorCodes.VerificationCodeTooManyAttempts));
    }

    private static (VerificationCode Code, string Digits) Issue() =>
        VerificationCode.Issue(Guid.CreateVersion7(), Guid.CreateVersion7(), VerificationCodePurpose.EmailChange, "ada@new.example", Now);
}
