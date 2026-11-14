namespace ControlPlane.Application.TermsVersions;

/// <summary>One version, and how many users may accept.</summary>
public sealed record TermsVersionDetailView(TermsVersionView Version, int ActiveUserCount);
