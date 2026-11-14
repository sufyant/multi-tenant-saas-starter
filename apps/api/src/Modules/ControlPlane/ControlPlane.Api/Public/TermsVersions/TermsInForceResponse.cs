namespace ControlPlane.Api.Public.TermsVersions;

/// <summary>The terms version in force: its id, the day it took effect, from 00:00 UTC, and the link to its text.</summary>
public sealed record TermsInForceResponse(Guid Id, DateOnly EffectiveDate, string Url);
