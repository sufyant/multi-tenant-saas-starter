namespace ControlPlane.Api.Platform.Tenants;

/// <summary>How many tenants are in each billing situation, without the search or the filters: the options of the billing filter (4.38).</summary>
public sealed record TenantBillingCountsResponse(int PaymentOverdue, int Contract);
