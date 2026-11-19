namespace Billing.Api.Tenant.BillingEmails;

/// <summary>The tenant's billing email (10.01): an address, or none, or an empty one, to remove it.</summary>
public sealed record SetBillingEmailRequest(string? Address);
