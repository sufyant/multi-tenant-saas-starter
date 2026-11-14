namespace ControlPlane.Api.Platform.TermsVersions;

/// <summary>One terms version, and how many users may accept it: tenant users, neither suspended nor deleted.</summary>
public sealed record TermsVersionDetailResponse(TermsVersionResponse Version, int ActiveUserCount);
