namespace ControlPlane.Api.Tenant.Usage;

/// <summary>The tenant's spending cap (11.03): 1 to 10 000 000 000 minor units excl. VAT a month, or null to remove it.</summary>
public sealed record SetSpendingCapRequest(long? Amount);
