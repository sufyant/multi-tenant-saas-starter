namespace ControlPlane.Api.Platform.TermsVersions;

/// <summary>
/// Every terms version, the latest day first, and how many users may accept: tenant users, neither suspended nor deleted. At most one
/// version is scheduled; while one is, a new version is refused (409 <c>terms_version.scheduled_exists</c>).
/// </summary>
public sealed record TermsOfUseResponse(IReadOnlyList<TermsVersionResponse> Versions, int ActiveUserCount);
