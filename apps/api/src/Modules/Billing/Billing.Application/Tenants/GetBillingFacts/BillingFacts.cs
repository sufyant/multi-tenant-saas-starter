namespace Billing.Application.Tenants.GetBillingFacts;

/// <summary>The tenant's name of this moment and our user id of the system admin who acts.</summary>
public sealed record BillingFacts(string TenantName, Guid ActorId);
