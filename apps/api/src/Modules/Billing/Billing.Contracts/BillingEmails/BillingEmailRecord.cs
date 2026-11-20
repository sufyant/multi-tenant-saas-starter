namespace Billing.Contracts.BillingEmails;

/// <summary>A tenant's billing email and the user id of the member who set it last (B14).</summary>
public sealed record BillingEmailRecord(string Address, Guid SetBy);
