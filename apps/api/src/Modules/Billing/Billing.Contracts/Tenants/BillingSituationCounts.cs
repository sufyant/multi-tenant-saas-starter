namespace Billing.Contracts.Tenants;

/// <summary>How many tenants are in each billing situation (4.38).</summary>
public sealed record BillingSituationCounts(int PaymentOverdue, int Contract);
