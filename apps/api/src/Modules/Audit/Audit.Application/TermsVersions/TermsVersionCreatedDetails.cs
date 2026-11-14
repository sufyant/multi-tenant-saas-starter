using Audit.Application.AuditLogs;

namespace Audit.Application.TermsVersions;

/// <summary>The details of <c>terms_version.created</c>: the version's values.</summary>
public sealed record TermsVersionCreatedDetails(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance) : AuditDetails;
