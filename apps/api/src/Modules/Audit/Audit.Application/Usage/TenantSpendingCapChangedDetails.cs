using Audit.Application.AuditLogs;

namespace Audit.Application.Usage;

/// <summary>
/// The details of <c>tenant.spending_cap_changed</c>, in the tenant's log: the cap before and after, in minor units excl. VAT of the
/// currency, each null for none (step 40c).
/// </summary>
public sealed record TenantSpendingCapChangedDetails(long? OldAmount, long? NewAmount, string Currency) : AuditDetails;
