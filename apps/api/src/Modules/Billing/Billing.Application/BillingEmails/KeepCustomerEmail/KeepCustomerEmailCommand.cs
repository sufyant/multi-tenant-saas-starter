using SharedKernel;

namespace Billing.Application.BillingEmails.KeepCustomerEmail;

/// <summary>Asks the provider to send the customer's invoices and receipts to this address (B14; Stripe, "Customer emails").</summary>
[MessageName("keep-customer-email")]
public sealed record KeepCustomerEmailCommand(string CustomerId, string Email);
