namespace Billing.Domain.Contracts;

/// <summary>
/// A Contract tenant (B12), listed across tenants for the platform's Organisations list and its billing filter (4.38). The mark itself
/// stays under the tenant's row level security; this row holds only the tenant, so it belongs to no tenant's row level security (R6), as
/// the payers do (B13). The tenant's own transaction writes it with the mark.
/// </summary>
internal sealed class ContractTenant(Guid tenantId)
{
    public Guid TenantId { get; private init; } = tenantId;
}
