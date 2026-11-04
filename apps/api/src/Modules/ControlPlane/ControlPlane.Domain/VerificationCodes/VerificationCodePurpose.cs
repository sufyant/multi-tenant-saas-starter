namespace ControlPlane.Domain.VerificationCodes;

/// <summary>What a verification code proves: the person reads the new address, or the person deletes their own account.</summary>
internal enum VerificationCodePurpose
{
    EmailChange,
    AccountDeletion,
}
