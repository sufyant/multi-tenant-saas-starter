using SharedKernel;

namespace Billing.Application.Usage.ChargeExtra;

/// <summary>Asks the provider to charge a meter's extra of a month on the subscription's next invoice (E11).</summary>
[MessageName("charge-extra")]
public sealed record ChargeExtraCommand(
    string CustomerId,
    string SubscriptionId,
    Guid TenantId,
    string MeterKey,
    DateOnly Period,
    long Extra,
    long Amount,
    string Currency);
