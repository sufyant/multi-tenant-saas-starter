namespace ControlPlane.Api.Me.TermsVersions;

/// <summary>
/// The link of the version the person read. A version whose link changed since, or that is no longer in force or scheduled, answers 409
/// <c>terms_version.changed</c>: the person reads it again.
/// </summary>
public sealed record AcceptTermsVersionRequest(string Url);
