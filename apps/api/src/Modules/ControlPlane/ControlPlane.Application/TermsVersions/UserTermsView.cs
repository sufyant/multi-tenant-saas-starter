namespace ControlPlane.Application.TermsVersions;

/// <summary>
/// Whether one user accepted the terms, for the user's page in the platform: the version in force, and the scheduled version when it
/// requires acceptance, each with the moment the user accepted it or none.
/// </summary>
public sealed record UserTermsView(UserTermsVersionView? InForce, UserTermsVersionView? Scheduled);
