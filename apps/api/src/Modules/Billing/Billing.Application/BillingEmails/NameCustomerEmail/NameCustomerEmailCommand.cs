using SharedKernel;

namespace Billing.Application.BillingEmails.NameCustomerEmail;

/// <summary>
/// The first step of the provider's customer email (B14): the billing email, or, with none, the address of the member who opened the last checkout or changed the plan last (the customer's member, B9), which
/// ControlPlane's contracts read on ControlPlane's DbContext, as one DbContext owns a handler's transaction (W1).
/// </summary>
[MessageName("name-customer-email")]
public sealed record NameCustomerEmailCommand(string CustomerId, string? Address, Guid PaidBy);
