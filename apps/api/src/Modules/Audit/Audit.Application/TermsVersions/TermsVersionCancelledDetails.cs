using Audit.Application.AuditLogs;

namespace Audit.Application.TermsVersions;

/// <summary>
/// The details of <c>terms_version.cancelled</c>: the version's day, link and whether it required acceptance, and how many users had
/// accepted it, whose acceptances stay with it.
/// </summary>
public sealed record TermsVersionCancelledDetails(DateOnly EffectiveDate, string Url, bool RequiresAcceptance, int AcceptanceCount) : AuditDetails;
