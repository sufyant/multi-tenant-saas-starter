using Audit.Application.AuditLogs;

namespace Audit.Application.BillingEmails;

/// <summary>The details of <c>tenant.billing_email_changed</c>, in the tenant's log: whether the member removed the address (B14).</summary>
public sealed record TenantBillingEmailChangedDetails(bool Removed) : AuditDetails;
