namespace Billing.Application.Tenants.GetTenantBilling;

/// <summary>The tenant's name of this moment and our user id of the system admin who acts.</summary>
public sealed record TenantBillingFacts(string TenantName, Guid ActorId);
