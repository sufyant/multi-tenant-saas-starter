using ControlPlane.Domain.Plans;
using ControlPlane.Domain.MeterUsages;

namespace ControlPlane.Application.Usage;

/// <summary>A tenant's use of its meters, for the command that counts a use (step 40c). The tenant is the transaction's (R4).</summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface IMeterUsageRepository
{
    /// <summary>
    /// The setting of the meter on the plan of the transaction's tenant, with the tenant row held <c>FOR SHARE</c> for the rest of the
    /// transaction, so a plan change, a closure and the purge wait for the use (K6q); none when the tenant is not active, or its plan has
    /// no setting for the meter.
    /// </summary>
    internal Task<MeterSetting?> FindSettingForShareAsync(string meterKey, CancellationToken cancellationToken);

    /// <summary>
    /// The tenant's use of the meter in the period, locked for the rest of the transaction, so two uses count one after the other. A
    /// period without a use yet starts at nothing.
    /// </summary>
    internal Task<MeterUsage> FindForUpdateAsync(string meterKey, DateOnly period, CancellationToken cancellationToken);

    /// <summary>
    /// What the extra of every other meter of the tenant costs in the period, at the price of each one's last use, in minor units before
    /// rounding (E9).
    /// </summary>
    internal Task<decimal> SumExtraAmountAsync(DateOnly period, string exceptMeterKey, CancellationToken cancellationToken);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
