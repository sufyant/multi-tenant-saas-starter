namespace ControlPlane.Api.Platform.TermsVersions;

/// <summary>
/// The values of the scheduled terms version, as <see cref="CreateTermsVersionRequest"/>. Once someone accepted it, its link and
/// <c>requiresAcceptance</c> stay as they are: another value answers 409 <c>terms_version.locked</c>. <c>sendsEmail</c> is required: off
/// may turn on, and the email then goes to every user who may accept; once on, it stays on, and off answers 409
/// <c>terms_version.email_sent</c>.
/// </summary>
public sealed record UpdateTermsVersionRequest(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance, bool SendsEmail);
