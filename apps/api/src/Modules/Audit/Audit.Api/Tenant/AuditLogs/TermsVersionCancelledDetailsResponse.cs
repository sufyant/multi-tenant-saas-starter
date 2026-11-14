namespace Audit.Api.Tenant.AuditLogs;

public sealed record TermsVersionCancelledDetailsResponse(DateOnly EffectiveDate, string Url, bool RequiresAcceptance, int AcceptanceCount) : AuditDetailsResponse;
