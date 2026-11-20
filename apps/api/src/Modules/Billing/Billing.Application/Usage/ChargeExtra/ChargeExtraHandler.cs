using Billing.Application.Provider;

namespace Billing.Application.Usage.ChargeExtra;

// The handler takes no DbContext, so no transaction is open during the provider's call. A call that fails is tried again
// (BillingProviderRetries); the charge's tenant, meter and month are its idempotency key, so a retry or a second delivery charges once.
public static class ChargeExtraHandler
{
    public static Task HandleAsync(ChargeExtraCommand command, IBillingProvider provider, CancellationToken cancellationToken) =>
        provider.AddExtraAsync(
            new ExtraCharge(command.CustomerId, command.SubscriptionId, command.TenantId, command.MeterKey, command.Period, command.Extra, command.Amount, command.Currency),
            cancellationToken);
}
