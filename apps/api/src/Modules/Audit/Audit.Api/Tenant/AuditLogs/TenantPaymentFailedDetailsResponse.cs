namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantPaymentFailedDetailsResponse(long AmountDue, string Currency, DateTimeOffset RetriesUntil) : AuditDetailsResponse;
