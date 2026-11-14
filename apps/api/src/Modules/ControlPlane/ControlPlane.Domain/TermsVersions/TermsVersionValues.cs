namespace ControlPlane.Domain.TermsVersions;

/// <summary>
/// The values a system admin gives a terms version: its day, its link, its summary, whether it requires acceptance and whether it emails the
/// users.
/// </summary>
internal sealed record TermsVersionValues(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance, bool SendsEmail);
