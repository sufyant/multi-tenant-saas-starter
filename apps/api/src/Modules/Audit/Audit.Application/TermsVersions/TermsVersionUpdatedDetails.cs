using Audit.Application.AuditLogs;

namespace Audit.Application.TermsVersions;

/// <summary>The details of <c>terms_version.updated</c>: the scheduled version's values before and after the edit.</summary>
public sealed record TermsVersionUpdatedDetails(AuditTermsVersion Old, AuditTermsVersion New) : AuditDetails;
