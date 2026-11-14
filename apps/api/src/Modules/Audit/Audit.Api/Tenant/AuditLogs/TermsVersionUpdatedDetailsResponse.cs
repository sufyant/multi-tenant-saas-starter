namespace Audit.Api.Tenant.AuditLogs;

public sealed record TermsVersionUpdatedDetailsResponse(AuditTermsVersionResponse Old, AuditTermsVersionResponse New) : AuditDetailsResponse;
