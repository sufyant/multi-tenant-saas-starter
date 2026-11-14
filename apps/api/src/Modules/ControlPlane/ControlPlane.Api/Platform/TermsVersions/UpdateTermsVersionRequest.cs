namespace ControlPlane.Api.Platform.TermsVersions;

/// <summary>
/// The values of the scheduled terms version, as <see cref="CreateTermsVersionRequest"/>. Once someone accepted it, its link and
/// <c>requiresAcceptance</c> stay as they are: another value answers 409 <c>terms_version.locked</c>.
/// </summary>
public sealed record UpdateTermsVersionRequest(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance);
