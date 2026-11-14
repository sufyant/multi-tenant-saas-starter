namespace ControlPlane.Application.TermsVersions;

/// <summary>Every version, the newest day first, and how many users may accept.</summary>
public sealed record TermsOfUseView(IReadOnlyList<TermsVersionView> Versions, int ActiveUserCount);
