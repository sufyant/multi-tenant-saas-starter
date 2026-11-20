using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Usage;
using ControlPlane.Domain.MeterUsages;
using Wolverine;

namespace ControlPlane.Application.Usage.CloseMeterMonth;

// E11: the month's row is locked, so a use of the month that is still in its transaction counts first. A month whose extra costs nothing
// after rounding tells nothing. The message is scheduled by the messaging tool's clock; one that arrives before the module's clock says
// the month ended is scheduled again for the month's end, as a later use could still add extra. A second delivery publishes a second event
// of the same month, which Billing charges once (ChargeExtraHandler).
public static class CloseMeterMonthHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        CloseMeterMonthCommand command,
        IMeterUsageRepository usages,
        BillingOptions billing,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (now < MeterUsage.EndOf(command.Period))
        {
            return [command.ScheduledAt(MeterUsage.EndOf(command.Period))];
        }

        var usage = await usages.FindForUpdateAsync(command.MeterKey, command.Period, cancellationToken);
        return usage.ExtraAmount > 0
            ? [new MeterMonthClosedEvent(Guid.CreateVersion7(now), now, command.TenantId, usage.MeterKey, usage.Period, usage.Extra, usage.ExtraAmount, billing.Currency)]
            : [];
    }
}
