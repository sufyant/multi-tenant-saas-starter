namespace ControlPlane.Domain.Tenants;

/// <summary>What to do with the identity provider's answer for an invitation's sign-up (<see cref="Invitation.RecordSignUp"/>).</summary>
internal enum SignUpDecision
{
    /// <summary>Send the invitation email with the link.</summary>
    Send,

    /// <summary>Send nothing: the answer arrived twice, or the invitation is no longer pending and nobody can sign up for it.</summary>
    Ignore,

    /// <summary>Send nothing and withdraw the provider's invitation: nobody receives its link.</summary>
    Withdraw,
}
