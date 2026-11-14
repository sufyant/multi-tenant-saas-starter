namespace ControlPlane.Domain.TermsVersions;

/// <summary>Where a terms version stands on a day (<see cref="TermsTimeline"/>).</summary>
internal enum TermsVersionState
{
    Scheduled,
    Current,
    Replaced,
    Cancelled,
}
