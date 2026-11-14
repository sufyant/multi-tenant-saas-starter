namespace ControlPlane.Api.Platform.TermsVersions;

/// <summary>
/// A new terms version: the day it takes effect, today or later, from 00:00 UTC; the link to its text, an absolute https URL; an optional
/// summary of what changed; and whether a person must accept it. The API trims the link and the summary before it counts their lengths.
/// </summary>
public sealed record CreateTermsVersionRequest(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance);
