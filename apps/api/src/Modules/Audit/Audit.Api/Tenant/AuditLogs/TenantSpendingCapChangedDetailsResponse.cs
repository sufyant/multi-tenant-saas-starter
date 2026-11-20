namespace Audit.Api.Tenant.AuditLogs;

public sealed record TenantSpendingCapChangedDetailsResponse(long? OldAmount, long? NewAmount, string Currency) : AuditDetailsResponse;
