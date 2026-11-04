namespace ControlPlane.Contracts.Users;

/// <summary>Which email carries a verification code: the one to a new address, or the one that deletes the account.</summary>
public enum VerificationEmailKind
{
    EmailChange,
    AccountDeletion,
}
