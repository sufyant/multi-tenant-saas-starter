namespace ControlPlane.Api.Platform.TermsVersions;

/// <summary>
/// A new terms version: the day it takes effect, today or later, from 00:00 UTC; the link to its text, an absolute https URL; an optional
/// summary of what changed; whether a person must accept it; and whether its creation emails every user who may accept, true when left out.
/// A version that emails nobody, a small change, gives no notice on Home either. The API trims the link and the summary before it counts
/// their lengths.
/// </summary>
public sealed record CreateTermsVersionRequest(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance, bool SendsEmail = true);
