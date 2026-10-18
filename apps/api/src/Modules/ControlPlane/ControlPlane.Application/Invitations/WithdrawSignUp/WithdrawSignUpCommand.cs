namespace ControlPlane.Application.Invitations.WithdrawSignUp;

/// <summary>Withdraws the identity provider's invitation: nobody can sign up with its link any more.</summary>
public sealed record WithdrawSignUpCommand(string SignUpInvitationId);
