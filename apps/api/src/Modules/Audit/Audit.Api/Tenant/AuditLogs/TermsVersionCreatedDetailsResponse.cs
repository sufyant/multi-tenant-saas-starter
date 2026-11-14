namespace Audit.Api.Tenant.AuditLogs;

public sealed record TermsVersionCreatedDetailsResponse(DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance) : AuditDetailsResponse;
