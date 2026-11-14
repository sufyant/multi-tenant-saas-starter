namespace ControlPlane.Contracts.TermsVersions;

/// <summary>The values of a terms version at one moment.</summary>
public sealed record TermsVersionRecord(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance);
