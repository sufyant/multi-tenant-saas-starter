using ControlPlane.Domain.Tenants;
using SharedKernel;

namespace ControlPlane.Application.Invitations.WithdrawSignUp;

/// <summary>Withdraws the identity provider's invitation: nobody can sign up with its link any more.</summary>
[MessageName("withdraw-sign-up")]
public sealed record WithdrawSignUpCommand(string SignUpInvitationId)
{
    /// <summary>The withdrawals of the invitations that ended: one for each that has an identity provider's invitation.</summary>
    internal static IEnumerable<WithdrawSignUpCommand> For(IEnumerable<Invitation> ended) =>
        ended.Where(invitation => invitation.SignUpInvitationId is not null).Select(invitation => new WithdrawSignUpCommand(invitation.SignUpInvitationId!));
}
