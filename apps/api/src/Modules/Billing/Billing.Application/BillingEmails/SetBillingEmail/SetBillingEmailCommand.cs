using SharedKernel;

namespace Billing.Application.BillingEmails.SetBillingEmail;

/// <summary>A member who manages billing sets the tenant's billing email (10.01, B14), or removes it with none.</summary>
[MessageName("set-billing-email")]
public sealed record SetBillingEmailCommand(Guid TenantId, string? Address, Guid SetBy);
