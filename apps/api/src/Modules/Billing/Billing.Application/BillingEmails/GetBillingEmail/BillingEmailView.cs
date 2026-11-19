namespace Billing.Application.BillingEmails.GetBillingEmail;

/// <summary>The tenant's billing email, and the user id of the member who set it last.</summary>
public sealed record BillingEmailView(string Address, Guid SetBy);
