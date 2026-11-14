namespace ControlPlane.Domain.TermsVersions;

/// <summary>What the gate after sign-in asks of a person (<see cref="TermsGate"/>).</summary>
internal enum TermsAction
{
    None,
    Accept,
    Notice,
}
