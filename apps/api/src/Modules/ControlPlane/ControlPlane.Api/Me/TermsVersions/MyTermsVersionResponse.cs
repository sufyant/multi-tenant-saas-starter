namespace ControlPlane.Api.Me.TermsVersions;

/// <summary>A terms version as the person reads it: its day, from 00:00 UTC, its link, its summary or null, and whether it requires acceptance.</summary>
public sealed record MyTermsVersionResponse(Guid Id, DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance);
