using Audit.Application.AuditLogs;

namespace Audit.Application.Plans;

/// <summary>
/// The details of <c>tenant.payment_failed</c>, in both logs: the amount due without tax, in the currency's minor unit, and until when the
/// provider retries the payment (B11).
/// </summary>
public sealed record TenantPaymentFailedDetails(long AmountDue, string Currency, DateTimeOffset RetriesUntil) : AuditDetails;
