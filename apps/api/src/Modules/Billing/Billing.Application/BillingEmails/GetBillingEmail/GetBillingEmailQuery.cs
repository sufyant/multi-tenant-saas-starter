using SharedKernel;

namespace Billing.Application.BillingEmails.GetBillingEmail;

/// <summary>The tenant's billing email and the member who set it (B14); none when it has none.</summary>
[MessageName("get-billing-email")]
public sealed record GetBillingEmailQuery;
