namespace ControlPlane.Contracts.TermsVersions;

/// <summary>The values of a terms version at one moment.</summary>
/// <param name="SendsEmail">Whether the version emails the users; true in a message of the version before.</param>
public sealed record TermsVersionRecord(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance, bool SendsEmail = true);
