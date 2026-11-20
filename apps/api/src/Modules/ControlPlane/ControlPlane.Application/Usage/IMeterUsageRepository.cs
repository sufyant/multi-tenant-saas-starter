using ControlPlane.Domain.Plans;
using ControlPlane.Domain.Usage;

namespace ControlPlane.Application.Usage;

/// <summary>A tenant's use of its meters, for the command that counts a use (step 40c). The tenant is the transaction's (R4).</summary>
/// <remarks>Public because a public handler takes it (W9); its members speak domain types, so they are internal to the module.</remarks>
public interface IMeterUsageRepository
{
    /// <summary>The setting of the meter on the plan of the transaction's tenant; none when the plan has no setting for it.</summary>
    internal Task<MeterSetting?> FindSettingAsync(string meterKey, CancellationToken cancellationToken);

    /// <summary>
    /// The tenant's use of the meter in the period, locked for the rest of the transaction, so two uses count one after the other. A
    /// period without a use yet starts at nothing.
    /// </summary>
    internal Task<MeterUsage> FindForUpdateAsync(string meterKey, DateOnly period, CancellationToken cancellationToken);

    internal Task SaveChangesAsync(CancellationToken cancellationToken);
}
