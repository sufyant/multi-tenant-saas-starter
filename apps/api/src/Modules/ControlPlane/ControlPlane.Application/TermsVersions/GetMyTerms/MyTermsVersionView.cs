namespace ControlPlane.Application.TermsVersions.GetMyTerms;

/// <summary>A terms version as the person reads it: its day, its link, its summary and whether it requires acceptance.</summary>
public sealed record MyTermsVersionView(Guid Id, DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance);
