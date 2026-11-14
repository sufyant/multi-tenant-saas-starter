namespace ControlPlane.Domain.Terms;

/// <summary>The values a system admin gives a terms version: its day, its link, its summary and whether it requires acceptance.</summary>
internal sealed record TermsVersionValues(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance);
