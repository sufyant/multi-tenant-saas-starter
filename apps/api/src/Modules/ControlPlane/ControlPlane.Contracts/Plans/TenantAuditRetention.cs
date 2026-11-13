namespace ControlPlane.Contracts.Plans;

/// <summary>A tenant's effective Activity log period at one moment, in days: none for no limit, which a missing value is too (E3).</summary>
public sealed record TenantAuditRetention(Guid TenantId, int? Days);
