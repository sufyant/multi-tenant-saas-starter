namespace Audit.Api.Tenant.AuditLogs;

public sealed record FeatureFlagPercentageSetDetailsResponse(int From, int To) : AuditDetailsResponse;
